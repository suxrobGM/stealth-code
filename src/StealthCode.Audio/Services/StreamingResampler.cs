namespace StealthCode.Audio.Services;

/// <summary>Linear resampler that carries interpolation state across packets.</summary>
internal sealed class StreamingResampler(int fromRate, int toRate)
{
    private readonly double inputStep = (double)fromRate / toRate;
    private readonly bool sameRate = fromRate == toRate;
    private float lastSample;
    private double position;

    /// <summary>Resamples a packet into <paramref name="output"/>, growing it as needed, and returns the sample count.</summary>
    public int Process(ReadOnlySpan<float> input, ref float[] output)
    {
        if (input.IsEmpty)
        {
            return 0;
        }

        if (sameRate)
        {
            if (output.Length < input.Length)
            {
                output = new float[input.Length];
            }

            input.CopyTo(output);
            return input.Length;
        }

        var capacity = (int)((input.Length - position) / inputStep) + 2;
        if (output.Length < capacity)
        {
            output = new float[capacity];
        }

        var count = 0;
        while (position < input.Length - 1)
        {
            var index = (int)Math.Floor(position);
            var fraction = (float)(position - index);
            var first = index < 0 ? lastSample : input[index];
            output[count++] = first + (input[index + 1] - first) * fraction;
            position += inputStep;
        }

        lastSample = input[^1];
        position -= input.Length;
        return count;
    }
}
