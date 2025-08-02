using LibResample.Sharp;

namespace LibStreamResampler;

/// <summary>
/// WIP. Works with signed 16bit samples only.
/// </summary>
public class StreamResampler
{
    public static StreamResampler Create(bool highQuality, int numChannels, int inputSampleRate, int outputSampleRate)
    {
        return new StreamResampler(highQuality, numChannels, inputSampleRate, outputSampleRate);
    }

    public static int GetExpectedOutputSize(int inputSize, float factor)
    {
        return (int)(inputSize * factor) + 400;
    }

    public static unsafe short[] ShortArrayFromBytes(byte* src, int srcSize)
    {
        int srcSamples = srcSize / SHORT_SIZE;
        short* shortArr = (short *)src;
        short[] resultArr = new short[srcSamples];
        fixed (short* resultPtr = resultArr)
        {
            Buffer.MemoryCopy(shortArr, resultPtr, srcSize, srcSize);
        }
        return resultArr;
    }

    /// <summary>
    /// Yes, sizeof(short) exists, but this is here as a reminder that current implementation works with
    /// signed 16bit samples only. It is work in progress.
    /// </summary>
    private const int SHORT_SIZE = 2;

    /// <summary>
    /// Value used by Apple (Core Audio)1, ALSA2, MatLab2, sndlib2.
    /// <para>[1] Link: <a href="https://web.archive.org/web/20210210064026/http://blog.bjornroche.com/2009/12/int-float-int-its-jungle-out-there.html">Int->Float->Int: It's a jungle out there! (Web Archive)</a></para>
    /// <para>[2] Link: <a href="http://www.mega-nerd.com/libsndfile/FAQ.html#Q010">Q10: Reading a 16 bit PCM file as normalised floats...</a></para>
    /// </summary>
    private const float CONVERT_FACTOR_SHORT = 32768.0f;

    private class ChannelResampler
    {
        public FloatBuffer Output { get { return _output; } }

        private ReSampler _resampler;

        private float[] _input = new float[1];

        private int _inputBufferUsed = 0;

        private FloatBuffer _output = FloatBuffer.Wrap(new float[1], 0, 1);

        public ChannelResampler(bool highQuality, float factor)
        {
            _resampler = new(highQuality, factor, factor);
        }

        public float[] PrepareInput(int samplesCount)
        {
            if (_input.Length < samplesCount)
            {
                _input = new float[samplesCount];
            }
            _inputBufferUsed = samplesCount;
            return _input;
        }

        public int ProcessInput(bool lastPacket, float factor)
        {
            _output.Position = 0;

            int expectedSize = GetExpectedOutputSize(_inputBufferUsed, factor);
            if (_output.Length < expectedSize)
            {
                _output = FloatBuffer.Wrap(new float[expectedSize], 0, expectedSize);
            }

            var inputBuffer = FloatBuffer.Wrap(_input, 0, _inputBufferUsed);
            var buffers = new ReSampler.SampleBuffers(inputBuffer, _output);
            _resampler.Process(factor, buffers, lastPacket);
            if (inputBuffer.RemainLength > 0)
            {
                throw new InvalidOperationException("Previous operation did not process all of input.");
            }

            return _output.Position;
        }
    }

    public float Factor { get { return _factor; } }

    public long InPacketCount { get { return _inPacketCount; } }

    public long InBytesProcessed { get { return _inBytesProcessed; } }

    public long OutBytesGenerated { get { return _outBytesGenerated; } }

    private int _numChannels;

    private float _factor;

    private ChannelResampler[] _cresArr;

    private byte[]? _outBuffer = null;

    private long _outSamplesReady = 0;

    private long _inPacketCount = 0;

    private long _inBytesProcessed = 0;

    private long _outBytesGenerated = 0;

    private StreamResampler(bool highQuality, int numChannels, int inputSampleRate, int outputSampleRate)
        : this(highQuality, numChannels, (float)outputSampleRate / (float)inputSampleRate)
    { }

    private StreamResampler(bool highQuality, int numChannels, float factor)
    {
        if (numChannels < 1 || numChannels > 2)
        {
            throw new ArgumentOutOfRangeException("Unsupported number of channels.");
        }

        _numChannels = numChannels;
        _factor = factor;
        _cresArr = new ChannelResampler[numChannels];
        if (numChannels == 1)
        {
            _cresArr[0] = new ChannelResampler(highQuality, factor);
        }
        else if (numChannels == 2)
        {
            _cresArr[0] = new ChannelResampler(highQuality, factor);
            _cresArr[1] = new ChannelResampler(highQuality, factor);
        }
    }

    public unsafe void Process(byte* input, int inputSize, bool lastPacket, Stream output)
    {
        Process(input, inputSize, lastPacket);
        WriteToStream(output);
    }

    public void Process(short[] input, bool lastPacket, Stream output)
    {
        Process(input, lastPacket);
        WriteToStream(output);
    }

    public unsafe void Process(byte* input, int inputSize, bool lastPacket)
    {
        ConvertAndPrepareInput(input, inputSize);
        ProcessInput(lastPacket);
        _inPacketCount++;
        _inBytesProcessed += inputSize;
    }

    public void Process(short[] input, bool lastPacket)
    {
        ConvertAndPrepareInput(input);
        ProcessInput(lastPacket);
        _inPacketCount++;
        _inBytesProcessed += input.Length * sizeof(short);
    }

    private void ProcessInput(bool lastPacket)
    {
        if (_numChannels == 1)
        {
            _outSamplesReady = _cresArr[0].ProcessInput(lastPacket, _factor);
        }
        else if (_numChannels == 2)
        {
            int outputSamples0 = _cresArr[0].ProcessInput(lastPacket, _factor);
            int outputSamples1 = _cresArr[1].ProcessInput(lastPacket, _factor);
            _outSamplesReady = Math.Min(outputSamples0, outputSamples1);
        }
    }

    private unsafe void ConvertAndPrepareInput(short[] input)
    {
        fixed (short* shortPtr = input)
        {
            ConvertAndPrepareInput((byte*)shortPtr, input.Length * SHORT_SIZE);
        }
    }

    private unsafe void ConvertAndPrepareInput(byte* input, int inputSize)
    {
        int srcSamples = inputSize / (SHORT_SIZE * _numChannels);
        short* shortArr = (short*)input;
        if (_numChannels == 1)
        {
            var input0 = _cresArr[0].PrepareInput(srcSamples);
            for (int i = 0; i < srcSamples; i++)
            {
                input0[i] = (float)shortArr[i] / CONVERT_FACTOR_SHORT;
            }
        }
        else if (_numChannels == 2)
        {
            var input0 = _cresArr[0].PrepareInput(srcSamples);
            var input1 = _cresArr[1].PrepareInput(srcSamples);
            for (int i = 0; i < srcSamples; i++)
            {
                input0[i] = (float)shortArr[2 * i + 0] / CONVERT_FACTOR_SHORT;
                input1[i] = (float)shortArr[2 * i + 1] / CONVERT_FACTOR_SHORT;
            }
        }
    }

    public long GetOutputSizeInBytes()
    {
        return _outSamplesReady * SHORT_SIZE * _numChannels;
    }

    public unsafe long ConvertAndFillOutput(ref byte[]? outputBuffer)
    {
        long outputBytes = GetOutputSizeInBytes();
        if (outputBuffer is null || outputBuffer.Length < outputBytes)
        {
            outputBuffer = new byte[outputBytes];
        }

        fixed (byte* bytePtr = outputBuffer)
        {
            return ConvertAndFillOutput(bytePtr);
        }
    }

    public unsafe long ConvertAndFillOutput(byte* outputBuffer)
    {
        short* shortPtr = (short*)outputBuffer;
        if (_numChannels == 1)
        {
            float[] floatArr0 = _cresArr[0].Output.Data;
            for (int i = 0; i < _outSamplesReady; i++)
            {
                shortPtr[i] = (short)(floatArr0[i] * CONVERT_FACTOR_SHORT);
            }
        }
        else if (_numChannels == 2)
        {
            float[] floatArr0 = _cresArr[0].Output.Data;
            float[] floatArr1 = _cresArr[1].Output.Data;
            for (int i = 0; i < _outSamplesReady; i++)
            {
                shortPtr[2 * i + 0] = (short)(floatArr0[i] * CONVERT_FACTOR_SHORT);
                shortPtr[2 * i + 1] = (short)(floatArr1[i] * CONVERT_FACTOR_SHORT);
            }
        }

        long result = _outSamplesReady * SHORT_SIZE * _numChannels;
        _outBytesGenerated += result;
        return result;
    }

    public void WriteToStream(Stream stream)
    {
        int bytesRead = (int)ConvertAndFillOutput(ref _outBuffer);
        if (_outBuffer != null)
        {
            stream.Write(_outBuffer, 0, bytesRead);
            _outBytesGenerated += bytesRead;
        }
#if DEBUG
        else
        {
            throw new InvalidOperationException("Previous operation did not generate any output.");
        }
#endif
    }
}
