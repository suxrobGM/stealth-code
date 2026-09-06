using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace StealthCode.Audio.Capture;

/// <summary>Decodes raw WASAPI PCM packets into mono float32 samples.</summary>
internal static class AudioConverter
{
    /// <summary>Sample rate Whisper expects.</summary>
    public const int WhisperSampleRate = 16000;

    /// <summary>Decodes a PCM packet into <paramref name="mono"/>, growing it as needed, and returns the sample count.</summary>
    public static int DecodeToMono(ReadOnlySpan<byte> pcm, CaptureFormat format, ref float[] mono)
    {
        var isFloat = format.IsFloat || format.BitsPerSample == 32;
        if (!isFloat && format.BitsPerSample != 16)
        {
            return 0;
        }

        var channels = Math.Max(format.Channels, 1);
        var bytesPerSample = isFloat ? 4 : 2;
        var frames = pcm.Length / (bytesPerSample * channels);
        if (frames == 0)
        {
            return 0;
        }

        if (mono.Length < frames)
        {
            mono = new float[frames];
        }

        var scale = 1f / channels;

        if (isFloat)
        {
            var samples = MemoryMarshal.Cast<byte, float>(pcm);
            for (var i = 0; i < frames; i++)
            {
                var sum = 0f;
                for (var ch = 0; ch < channels; ch++)
                {
                    sum += samples[i * channels + ch];
                }

                mono[i] = sum * scale;
            }

            return frames;
        }

        for (var i = 0; i < frames; i++)
        {
            var sum = 0f;
            for (var ch = 0; ch < channels; ch++)
            {
                sum += BinaryPrimitives.ReadInt16LittleEndian(pcm[((i * channels + ch) * 2)..]) / 32768f;
            }

            mono[i] = sum * scale;
        }

        return frames;
    }
}
