using System.Runtime.Versioning;
using System.Text;
using System.Threading.Channels;
using StealthCode.Audio.Models;

namespace StealthCode.Audio.Services;

/// <summary>What the live transcription pipeline is doing right now.</summary>
public enum LiveTranscriptionState
{
    Idle,
    LoadingModel,
    Listening,
    Hearing,
    Transcribing
}

/// <summary>Streams loopback audio through Whisper and reports transcripts as speech ends.</summary>
/// <remarks>Events are raised on worker threads.</remarks>
[SupportedOSPlatform("windows")]
public sealed class LiveTranscriptionService(TranscriptionService transcription) : IDisposable
{
    private const int SilenceCheckIntervalMs = 100;
    private const int StopTimeoutMs = 15000;
    private const int ContextTextLength = 200;

    private readonly WasapiLoopbackCapture loopback = new();
    private Channel<byte[]>? packets;
    private Channel<SpeechChunk>? chunks;
    private SpeechSegmenter? segmenter;
    private CancellationTokenSource? cancellation;
    private Task? segmenterTask;
    private Task? workerTask;
    private LiveTranscriptionState state;
    private volatile bool transcribing;
    private bool starting;
    private bool stopping;

    /// <summary>True once capture and transcription are running.</summary>
    public bool IsListening { get; private set; }

    /// <summary>True when a GPU runtime was skipped and the CPU used instead.</summary>
    public bool GpuFellBack => transcription.GpuFellBack;

    /// <summary>The last failure message, or null.</summary>
    public string? LastError { get; private set; }

    public event Action<LiveTranscriptionState>? StateChanged;

    /// <summary>Text of the current utterance so far.</summary>
    public event Action<string>? PartialTranscript;

    /// <summary>A finished utterance, never empty.</summary>
    public event Action<string>? UtteranceCompleted;

    public event Action<string>? Failed;

    /// <summary>Loads the model and starts listening. Returns false when the model could not be loaded.</summary>
    public async Task<bool> StartAsync(AudioSettings settings)
    {
        if (IsListening || starting || stopping)
        {
            return false;
        }

        starting = true;

        try
        {
            LastError = null;
            SetState(LiveTranscriptionState.LoadingModel);

            var error = await transcription.LoadAsync(settings);
            if (error is not null)
            {
                LastError = error;
                SetState(LiveTranscriptionState.Idle);
                Failed?.Invoke(error);
                return false;
            }

            cancellation = new CancellationTokenSource();
            await transcription.WarmUpAsync(cancellation.Token);

            var options = new UnboundedChannelOptions { SingleReader = true, SingleWriter = true };
            packets = Channel.CreateUnbounded<byte[]>(options);
            chunks = Channel.CreateUnbounded<SpeechChunk>(options);

            var writer = chunks.Writer;
            segmenter = new SpeechSegmenter(
                new SpeechSegmenterOptions(EndOfUtteranceMs: settings.EndOfUtteranceMs),
                chunk => writer.TryWrite(chunk),
                OnSpeechActiveChanged);

            IsListening = true;
            loopback.Start(packets.Writer);
            segmenterTask = Task.Run(() => RunSegmenterAsync(cancellation.Token), CancellationToken.None);
            workerTask = Task.Run(() => RunWorkerAsync(cancellation.Token), CancellationToken.None);

            SetState(LiveTranscriptionState.Listening);
            return true;
        }
        finally
        {
            starting = false;
        }
    }

    /// <summary>Stops capture and drains the pending chunks, so a final utterance can still arrive.</summary>
    public async Task StopAsync()
    {
        if (!IsListening || stopping)
        {
            return;
        }

        stopping = true;

        try
        {
            loopback.Stop();

            if (segmenterTask is not null)
            {
                await segmenterTask;
            }

            if (workerTask is not null && await Task.WhenAny(workerTask, Task.Delay(StopTimeoutMs)) != workerTask)
            {
                cancellation?.Cancel();
                await workerTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the drain timed out.
        }
        finally
        {
            cancellation?.Dispose();
            cancellation = null;
            segmenterTask = null;
            workerTask = null;
            transcribing = false;
            IsListening = false;
            SetState(LiveTranscriptionState.Idle);
            stopping = false;
        }
    }

    public void Dispose()
    {
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private async Task RunSegmenterAsync(CancellationToken ct)
    {
        var reader = packets!.Reader;
        var mono = new float[4096];
        var resampled = new float[4096];
        StreamingResampler? resampler = null;
        string? failure = null;

        try
        {
            while (true)
            {
                using var waitTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                waitTimeout.CancelAfter(SilenceCheckIntervalMs);

                try
                {
                    if (!await reader.WaitToReadAsync(waitTimeout.Token))
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    segmenter!.AdvanceSilence(SilenceCheckIntervalMs);
                    continue;
                }

                while (reader.TryRead(out var packet))
                {
                    if (loopback.Format is not { } format)
                    {
                        continue;
                    }

                    resampler ??= new StreamingResampler(format.SampleRate, AudioConverter.WhisperSampleRate);

                    var count = AudioConverter.DecodeToMono(packet, format, ref mono);
                    if (count == 0)
                    {
                        continue;
                    }

                    var resampledCount = resampler.Process(mono.AsSpan(0, count), ref resampled);
                    segmenter!.Push(resampled.AsSpan(0, resampledCount));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the pipeline is cancelled.
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }

        segmenter!.Flush();
        chunks!.Writer.TryComplete();

        if (failure is not null)
        {
            LastError = failure;
            Failed?.Invoke(failure);
        }
    }

    private async Task RunWorkerAsync(CancellationToken ct)
    {
        var utterance = new StringBuilder();
        var contextText = string.Empty;

        try
        {
            await foreach (var chunk in chunks!.Reader.ReadAllAsync(ct))
            {
                if (chunk.EndsUtterance)
                {
                    var utteranceText = utterance.ToString().Trim();
                    utterance.Clear();
                    contextText = string.Empty;

                    if (utteranceText.Length > 0)
                    {
                        UtteranceCompleted?.Invoke(utteranceText);
                    }

                    continue;
                }

                transcribing = true;
                SetState(LiveTranscriptionState.Transcribing);

                try
                {
                    var text = await transcription.TranscribeAsync(chunk.Samples, contextText, ct);
                    if (text.Length > 0)
                    {
                        if (utterance.Length > 0)
                        {
                            utterance.Append(' ');
                        }

                        utterance.Append(text);
                        contextText = text.Length > ContextTextLength ? text[^ContextTextLength..] : text;
                        PartialTranscript?.Invoke(utterance.ToString());
                    }
                }
                finally
                {
                    transcribing = false;
                }

                SetState(segmenter!.SpeechActive ? LiveTranscriptionState.Hearing : LiveTranscriptionState.Listening);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the drain timed out.
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Failed?.Invoke(ex.Message);
        }
    }

    private void OnSpeechActiveChanged(bool active)
    {
        if (transcribing)
        {
            return;
        }

        SetState(active ? LiveTranscriptionState.Hearing : LiveTranscriptionState.Listening);
    }

    private void SetState(LiveTranscriptionState next)
    {
        if (state == next)
        {
            return;
        }

        state = next;
        StateChanged?.Invoke(next);
    }
}
