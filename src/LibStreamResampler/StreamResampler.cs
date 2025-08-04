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

    private unsafe class ChannelResampler : ReSampler.ISampleBuffers
    {
        private ReSampler _resampler;

        private int _ioff;

        private int _istep;

        private short* _input = null;

        private short* _output = null;

        private long _inputSampleCount = 0;

        private long _inputSamplesUsed = 0;

        private long _outputSampleMaxCount = 0;

        private long _outputSampleCount = 0;

        public ChannelResampler(bool highQuality, float factor, int ioff, int istep)
        {
            _resampler = new(highQuality, factor, factor);
            _ioff = ioff;
            _istep = istep;
        }

        public unsafe long ProcessInput(float factor, bool lastPacket, byte* input, long inputSize, byte* output, long outputSize)
        {
            _input = (short *)input;
            _output = (short*)output;
            _inputSamplesUsed = 0;
            _inputSampleCount = inputSize / 2;
            _outputSampleCount = 0;
            _outputSampleMaxCount = outputSize / 2;

            _resampler.Process(factor, this, lastPacket);
            if (_inputSamplesUsed < _inputSampleCount)
            {
                throw new InvalidOperationException("Previous operation did not process all of input.");
            }

            return _outputSampleCount * 2;
        }

        //
        // ISampleBuffers interface members.
        //

        public int GetInputBufferLenght()
        {
            return (int)(_inputSampleCount - _inputSamplesUsed);
        }

        public int GetOutputBufferLength()
        {
            return (int)(_outputSampleMaxCount - _outputSampleCount);
        }

        public void ProduceInput(float[] array, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                int index = (int)(_ioff + ((_inputSamplesUsed + i) * _istep));
                array[offset + i] = _input[index] / CONVERT_FACTOR_SHORT;
            }
            _inputSamplesUsed += length;
        }

        public void ConsumeOutput(float[] array, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                int index = (int)(_ioff + ((_outputSampleCount + i) * _istep));
                _output[index] = (short)(array[offset + i] * CONVERT_FACTOR_SHORT);
            }
            _outputSampleCount += length;
        }
    }

    public float Factor { get { return _factor; } }

    public long InPacketCount { get { return _inPacketCount; } }

    public long InBytesProcessed { get { return _inBytesProcessed; } }

    public long OutBytesGenerated { get { return _outBytesGenerated; } }

    private int _numChannels;

    private float _factor;

    private ChannelResampler[] _cresArr;

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
            _cresArr[0] = new ChannelResampler(highQuality, factor, 0, 1);
        }
        else if (numChannels == 2)
        {
            _cresArr[0] = new ChannelResampler(highQuality, factor, 0, 2);
            _cresArr[1] = new ChannelResampler(highQuality, factor, 1, 2);
        }
    }

    public unsafe long Process(byte* input, long inputSize, bool lastPacket, byte* output, long outputSize)
    {
        long outputUsed = 0;
        for (int i=0; i < _numChannels; i++)
        {
            // TODO(?): Assert that all channels returned the same value.
            // TODO(?): Process in parallel (async).
            outputUsed += _cresArr[i].ProcessInput(_factor, lastPacket, input, inputSize / _numChannels, output, outputSize / _numChannels);
        }

        _inPacketCount++;
        _inBytesProcessed += inputSize;
        return outputUsed;
    }


    public long GetOutputSizeInBytes()
    {
        return _outSamplesReady * SHORT_SIZE * _numChannels;
    }

}
