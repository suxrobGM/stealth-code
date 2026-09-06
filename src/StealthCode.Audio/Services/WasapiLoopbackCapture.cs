using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using System.Threading.Channels;
using StealthCode.Audio.Interop;

namespace StealthCode.Audio.Services;

/// <summary>
/// Audio format metadata returned by WASAPI after initializing the loopback stream.
/// </summary>
internal sealed record CaptureFormat(int SampleRate, int Channels, int BitsPerSample, bool IsFloat);

/// <summary>
/// Low-level WASAPI loopback capture. Opens the default render endpoint in loopback mode
/// and writes raw PCM packets to the provided channel on a background thread.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WasapiLoopbackCapture
{
    private Thread? captureThread;
    private volatile bool recording;

    /// <summary>
    /// The audio format negotiated with WASAPI. Available after capture starts.
    /// </summary>
    public CaptureFormat? Format { get; private set; }

    /// <summary>
    /// The last error message if capture failed, or null if successful.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Begins capturing loopback audio on a background thread, writing raw PCM to <paramref name="packets"/>.
    /// </summary>
    public void Start(ChannelWriter<byte[]> packets)
    {
        LastError = null;
        recording = true;
        captureThread = new Thread(() => CaptureLoop(packets))
        {
            IsBackground = true,
            Name = "WASAPI-Loopback"
        };
        captureThread.Start();
    }

    /// <summary>
    /// Stops the capture thread and waits up to 3 seconds for it to finish.
    /// </summary>
    public void Stop()
    {
        recording = false;
        captureThread?.Join(3000);
        captureThread = null;
    }

    /// <summary>
    /// Initializes WASAPI for loopback capture, then continuously reads audio packets and writes raw PCM data to the channel until recording is stopped or an error occurs.
    /// All COM objects are released and the channel is completed when done.
    /// </summary>
    private unsafe void CaptureLoop(ChannelWriter<byte[]> packets)
    {
        nint devicePtr = 0;
        nint audioClientPtr = 0;
        nint captureClientPtr = 0;
        nint mixFormatPtr = 0;

        try
        {
            var enumerator = CreateDeviceEnumerator();

            var hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Console, out devicePtr);
            ThrowIfFailed(hr, "Failed to get default audio endpoint");

            var device = ComInterfaceMarshaller<IMMDevice>.ConvertToManaged((void*)devicePtr)!;

            hr = device.Activate(in WasapiInterop.IID_IAudioClient, WasapiInterop.CLSCTX_ALL, 0, out audioClientPtr);
            ThrowIfFailed(hr, "Failed to activate audio client");

            var audioClient = ComInterfaceMarshaller<IAudioClient>.ConvertToManaged((void*)audioClientPtr)!;

            hr = audioClient.GetMixFormat(out mixFormatPtr);
            ThrowIfFailed(hr, "Failed to get mix format");

            Format = ParseFormat(mixFormatPtr);

            hr = audioClient.Initialize(
                AudioClientShareMode.Shared,
                WasapiInterop.AUDCLNT_STREAMFLAGS_LOOPBACK,
                2_000_000, 0, mixFormatPtr, 0);
            ThrowIfFailed(hr, "Failed to initialize audio client");

            hr = audioClient.GetService(in WasapiInterop.IID_IAudioCaptureClient, out captureClientPtr);
            ThrowIfFailed(hr, "Failed to get capture client");

            var captureClient = ComInterfaceMarshaller<IAudioCaptureClient>.ConvertToManaged((void*)captureClientPtr)!;
            var frameSize = Format.Channels * Format.BitsPerSample / 8;

            hr = audioClient.Start();
            ThrowIfFailed(hr, "Failed to start audio client");

            ReadLoop(captureClient, packets, frameSize);

            audioClient.Stop();
            audioClient.Reset();
        }
        catch (Exception ex)
        {
            LastError = $"Failed to start audio capture: {ex.Message}";
        }
        finally
        {
            ReleaseIfSet(mixFormatPtr, free: true);
            ReleaseIfSet(captureClientPtr);
            ReleaseIfSet(audioClientPtr);
            ReleaseIfSet(devicePtr);
            packets.TryComplete(LastError is null ? null : new InvalidOperationException(LastError));
        }
    }

    /// <summary>
    /// Continuously reads audio packets from the capture client and writes raw PCM data to the channel until recording is stopped.
    /// </summary>
    private void ReadLoop(IAudioCaptureClient captureClient, ChannelWriter<byte[]> packets, int frameSize)
    {
        while (recording)
        {
            var hr = captureClient.GetNextPacketSize(out var packetSize);
            if (hr < 0)
            {
                break;
            }

            if (packetSize == 0)
            {
                Thread.Sleep(10);
                continue;
            }

            hr = captureClient.GetBuffer(out var dataPtr, out var numFrames, out var flags, out _, out _);
            if (hr < 0)
            {
                break;
            }

            if (numFrames > 0)
            {
                var byteCount = (int)(numFrames * frameSize);
                var buffer = new byte[byteCount];

                if ((flags & WasapiInterop.AUDCLNT_BUFFERFLAGS_SILENT) == 0)
                {
                    Marshal.Copy(dataPtr, buffer, 0, byteCount);
                }

                packets.TryWrite(buffer);
            }

            captureClient.ReleaseBuffer(numFrames);
        }
    }

    /// <summary>
    /// Creates the MMDeviceEnumerator COM object used to find audio endpoints. This is done manually via CoCreateInstance
    /// to avoid adding a reference to the entire Windows SDK or using COM interop attributes on the interface definitions,
    /// which can cause issues with .NET's COM marshaller in a single-file app
    /// </summary>
    private static unsafe IMMDeviceEnumerator CreateDeviceEnumerator()
    {
        var hr = WasapiInterop.CoCreateInstance(
            in WasapiInterop.CLSID_MMDeviceEnumerator, 0, WasapiInterop.CLSCTX_ALL,
            typeof(IMMDeviceEnumerator).GUID, out nint ptr);
        ThrowIfFailed(hr, "Failed to create MMDeviceEnumerator");

        var enumerator = ComInterfaceMarshaller<IMMDeviceEnumerator>.ConvertToManaged((void*)ptr)!;
        Marshal.Release(ptr);
        return enumerator;
    }

    /// <summary>
    /// Parses the WAVEFORMATEX or WAVEFORMATEXTENSIBLE structure returned by WASAPI to extract audio format details.
    /// </summary>
    private static CaptureFormat ParseFormat(nint formatPtr)
    {
        var fmt = Marshal.PtrToStructure<WaveFormatEx>(formatPtr);
        var isFloat = fmt.FormatTag switch
        {
            WaveFormatEx.WAVE_FORMAT_IEEE_FLOAT => true,
            WaveFormatEx.WAVE_FORMAT_EXTENSIBLE =>
                Marshal.PtrToStructure<WaveFormatExtensible>(formatPtr).SubFormat ==
                WaveFormatExtensible.KSDATAFORMAT_SUBTYPE_IEEE_FLOAT,
            _ => false
        };

        return new CaptureFormat((int)fmt.SampleRate, fmt.Channels, fmt.BitsPerSample, isFloat);
    }

    private static void ThrowIfFailed(int hr, string message)
    {
        if (hr < 0)
        {
            throw Marshal.GetExceptionForHR(hr) ?? new COMException(message, hr);
        }
    }

    private static void ReleaseIfSet(nint ptr, bool free = false)
    {
        if (ptr == 0)
        {
            return;
        }

        if (free)
        {
            Marshal.FreeCoTaskMem(ptr);
        }
        else
        {
            Marshal.Release(ptr);
        }
    }
}
