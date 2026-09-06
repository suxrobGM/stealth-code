namespace StealthCode.Audio.Services;

/// <summary>A span of speech to transcribe, or a marker that the utterance ended.</summary>
public sealed record SpeechChunk(float[] Samples, bool EndsUtterance);

/// <summary>Splits a 16 kHz mono stream into speech chunks and utterance boundaries.</summary>
internal sealed class SpeechSegmenter(
    int endOfUtteranceMs,
    Action<SpeechChunk> emit,
    Action speechActiveChanged)
{
    private const int SpeechThresholdDb = -40;
    private const int SilenceMarginDb = 6;
    private const int LeadInMs = 300;
    private const int MinSpeechMs = 300;
    private const int ChunkSplitSilenceMs = 700;
    private const int MaxChunkMs = 15000;
    private const int FrameMs = 20;
    private const int FrameSamples = AudioConverter.WhisperSampleRate / 1000 * FrameMs;
    private const int LeadInFrames = LeadInMs / FrameMs;

    private static readonly float[] SilentFrame = new float[FrameSamples];

    private enum State
    {
        Idle,
        Speech,
        Trailing
    }

    private readonly float[] frame = new float[FrameSamples];
    private readonly float[] leadInBuffer = new float[LeadInFrames * FrameSamples];
    private readonly List<float> chunk = [];
    private State state;
    private int frameSampleCount;
    private int leadInStart;
    private int leadInCount;
    private int chunkMs;
    private int chunkSpeechMs;
    private int silenceMs;
    private bool isSpeech;
    private bool hasEmittedChunk;

    /// <summary>True while speech is being appended to the current chunk.</summary>
    public bool SpeechActive => state == State.Speech;

    /// <summary>Feeds captured samples, emitting chunks as they complete.</summary>
    public void Push(ReadOnlySpan<float> samples16k)
    {
        while (!samples16k.IsEmpty)
        {
            var take = Math.Min(FrameSamples - frameSampleCount, samples16k.Length);
            samples16k[..take].CopyTo(frame.AsSpan(frameSampleCount));
            frameSampleCount += take;
            samples16k = samples16k[take..];

            if (frameSampleCount == FrameSamples)
            {
                frameSampleCount = 0;
                ProcessFrame(frame);
            }
        }
    }

    /// <summary>Feeds silence for wall-clock time that passed without any packets.</summary>
    public void AdvanceSilence(int elapsedMs)
    {
        for (var remaining = elapsedMs; remaining >= FrameMs; remaining -= FrameMs)
        {
            ProcessFrame(SilentFrame);
        }
    }

    /// <summary>Emits the pending chunk and ends the utterance.</summary>
    public void Flush()
    {
        if (frameSampleCount > 0)
        {
            frame.AsSpan(frameSampleCount).Clear();
            frameSampleCount = 0;
            ProcessFrame(frame);
        }

        if (state == State.Speech)
        {
            speechActiveChanged();
        }

        state = State.Idle;
        EmitChunk();
        EndUtterance();
        leadInStart = 0;
        leadInCount = 0;
        isSpeech = false;
    }

    private void ProcessFrame(ReadOnlySpan<float> samples)
    {
        var loudness = LoudnessDb(samples);
        isSpeech = isSpeech
            ? loudness >= SpeechThresholdDb - SilenceMarginDb
            : loudness >= SpeechThresholdDb;

        switch (state)
        {
            case State.Idle:
                if (isSpeech)
                {
                    EnterSpeech(samples);
                }
                else
                {
                    StoreLeadIn(samples);
                }

                break;

            case State.Speech:
                Append(samples);

                if (isSpeech)
                {
                    silenceMs = 0;
                    chunkSpeechMs += FrameMs;
                }
                else
                {
                    silenceMs += FrameMs;
                    if (silenceMs >= ChunkSplitSilenceMs)
                    {
                        EmitChunk();
                        state = State.Trailing;
                        speechActiveChanged();
                        break;
                    }
                }

                if (chunkMs >= MaxChunkMs)
                {
                    EmitChunk();
                }

                break;

            case State.Trailing:
                if (isSpeech)
                {
                    EnterSpeech(samples);
                    break;
                }

                StoreLeadIn(samples);
                silenceMs += FrameMs;
                if (silenceMs >= endOfUtteranceMs)
                {
                    EndUtterance();
                    state = State.Idle;
                }

                break;
        }
    }

    private void EnterSpeech(ReadOnlySpan<float> samples)
    {
        AppendLeadIn();
        Append(samples);
        chunkSpeechMs += FrameMs;
        silenceMs = 0;
        state = State.Speech;
        speechActiveChanged();
    }

    private void Append(ReadOnlySpan<float> samples)
    {
        chunk.AddRange(samples);
        chunkMs += FrameMs;
    }

    private void StoreLeadIn(ReadOnlySpan<float> samples)
    {
        var slot = (leadInStart + leadInCount) % LeadInFrames;
        samples.CopyTo(leadInBuffer.AsSpan(slot * FrameSamples, FrameSamples));

        if (leadInCount == LeadInFrames)
        {
            leadInStart = (leadInStart + 1) % LeadInFrames;
        }
        else
        {
            leadInCount++;
        }
    }

    private void AppendLeadIn()
    {
        for (var i = 0; i < leadInCount; i++)
        {
            var slot = (leadInStart + i) % LeadInFrames;
            chunk.AddRange(leadInBuffer.AsSpan(slot * FrameSamples, FrameSamples));
        }

        chunkMs += leadInCount * FrameMs;
        leadInStart = 0;
        leadInCount = 0;
    }

    private void EmitChunk()
    {
        if (chunk.Count > 0 && chunkSpeechMs >= MinSpeechMs)
        {
            emit(new SpeechChunk([.. chunk], false));
            hasEmittedChunk = true;
        }

        chunk.Clear();
        chunkMs = 0;
        chunkSpeechMs = 0;
    }

    private void EndUtterance()
    {
        if (hasEmittedChunk)
        {
            emit(new SpeechChunk([], true));
        }

        hasEmittedChunk = false;
        silenceMs = 0;
    }

    private static float LoudnessDb(ReadOnlySpan<float> samples)
    {
        var sum = 0d;
        foreach (var sample in samples)
        {
            sum += sample * (double)sample;
        }

        return (float)(20d * Math.Log10(Math.Max(Math.Sqrt(sum / samples.Length), 1e-9d)));
    }
}
