/******************************************************************************
 *
 * libresamplesharp
 * Copyright (c) 2018 Xianyi Cui
 *
 * libresample4j
 * Copyright (c) 2009 Laszlo Systems, Inc
 *
 * libresample 0.1.3
 * Copyright (c) 2003 Dominic Mazzoni
 *
 * Resample 1.7
 * Copyright (c) 1994-2002 Julius O. Smith III
 * 
 * 'libresamplesharp' is a C# port of David Nault's 'libresample4j',
 * which is a Java port of Dominic Mazzoni's 'libresample 0.1.3',
 * which is in turn based on Julius Smith's 'Resample 1.7' library.
 *
 * This product includes software derived from the work of
 * Julius Smith, Dominic Mazzoni, David Nault, and others.
 * 
 * - https://ccrma.stanford.edu/~jos/resample/
 * - https://ccrma.stanford.edu/~jos/resample/Free_Resampling_Software.html
 * - https://github.com/minorninth/libresample
 * - https://github.com/dnault/libresample4j
 * - https://github.com/xycui/libresamplesharp
 *
 *
 * This file is part of AudioFormatLib, which is free software:
 * you can redistribute it and/or modify it under the terms of the
 * GNU Lesser General Public License (LGPL), version 2.1 only,
 * as published by the Free Software Foundation.
 *
 * AudioFormatLib is intended to be used as a dynamically linked library.
 * Applications using this library are not subject to the LGPL license,
 * provided they comply with its terms (e.g., allowing replacement of the library).
 *
 * AudioFormatLib is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * version 2.1 along with this file. If not, see:
 * https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html 
 *
 *****************************************************************************/

namespace LibStreamResampler;


/// <summary>
/// Use one of following static members to create an instance of resampler:
/// <list type="bullet">
///     <item> To work with byte type inputs and outputs: <see cref="StreamResampler.NewBytePacketResampler"/> </item>
///     <item> To work with short (signed 16-bit) type inputs and outputs: <see cref="StreamResampler.NewShortPacketResampler"/> </item>
///     <item> To work with float type inputs and outputs: <see cref="StreamResampler.NewFloatPacketResampler"/> </item>
/// </list>
/// 
/// Parameters:
/// <list type="bullet">
///     <item>highQuality - If false it will use lower quality algorithm, but faster.</item>
///     <item>factor - Ratio between input and output sample rate. If it is zero then both input and output sample rate parameters must be provided.</item>
///     <item>inputSampleRate - Can be zero if parameter 'factor' is non-zero value.</item>
///     <item>outputSampleRate - Can be zero if parameter 'factor' is non-zero value.</item>
///     <item>numChannels - Number of audio channels in input stream. Supported values aree 1 and 2.</item>
///     <item>sampleFormat - Input sample format. Supported value is <see cref="StreamResampler.SAMPLE_FMT_S16"/>, signed 16-bit integer.</item>
///     <item>outSampleFormat - Default is 0, the same as input sample format.</item>
///     <item>outChannels - Default is 0, the same as number of channels at input.</item>
/// </list>
/// FYI: Some of input/output conversions are still work in progress.
/// </summary>
public class StreamResampler 
    : StreamResampler.IBytePacketResampler
    , StreamResampler.IShortPacketResampler
    , StreamResampler.IFloatPacketResampler
{
    public const int SAMPLE_FMT_NONE = 0; // Defaults to input sample format.
    public const int SAMPLE_FMT_S16 = 1; // Signed 16 bit sample format.


    public interface IStreamResamplerInfo
    {
        bool HighQuality { get; }

        float Factor { get; }

        int InputSampleRate { get; }

        int OutputSampleRate { get; }

        int NumChannels { get; }

        int SampleFormat { get; }

        int OutputSampleFormat { get; }

        int OutputChannels { get; }

        long InPacketCount { get; }

        long InBytesProcessed { get; }

        long OutBytesGenerated { get; }
    }


    /// <summary>
    /// 
    /// Interface used to work with byte type inputs and outputs.
    /// 
    /// </summary>
    public interface IBytePacketResampler : IStreamResamplerInfo, IDisposable
    {
        unsafe long Process(bool lastPacket, byte* input, long inputSize, byte* output, long outputSize);

        long Process(bool lastPacket, byte[] input, long inputSize, byte[] output, long outputSize);

        long ProcessToShort(bool lastPacket, byte[] input, long inputSize, ref short[]? output);
    }


    /// <summary>
    /// 
    /// Create instance of resampler and return its <see cref="IBytePacketResampler"/> interface.
    /// 
    /// <para>See summary for <see cref="StreamResampler"/> class for description of parameters.</para>
    /// </summary>
    public static IBytePacketResampler NewBytePacketResampler(bool highQuality,
                                                              float factor,
                                                              int inputSampleRate = 0,
                                                              int outputSampleRate = 0,
                                                              int numChannels = 1, 
                                                              int sampleFormat = SAMPLE_FMT_S16,
                                                              int outSampleFormat = SAMPLE_FMT_NONE,
                                                              int outChannels = 0)
    {
        return new StreamResampler(highQuality, factor, inputSampleRate, outputSampleRate, numChannels, sampleFormat, outSampleFormat, outChannels);
    }


    /// <summary>
    /// 
    /// Interface used to work with short type inputs and outputs.
    /// 
    /// </summary>
    public interface IShortPacketResampler : IStreamResamplerInfo, IDisposable
    {
        unsafe long Process(bool lastPacket, short* input, long inputSize, short* output, long outputSize);

        long Process(bool lastPacket, short[] input, long inputSize, short[] output, long outputSize);

        long ProcessToByte(bool lastPacket, short[] input, long inputSize, ref byte[]? output);
    }


    /// <summary>
    /// 
    /// Create instance of resampler and return its <see cref="IShortPacketResampler"/> interface.
    /// 
    /// <para>See summary for <see cref="StreamResampler"/> class for description of parameters.</para>
    /// </summary>
    public static IShortPacketResampler NewShortPacketResampler(bool highQuality,
                                                                float factor,
                                                                int inputSampleRate = 0,
                                                                int outputSampleRate = 0,
                                                                int numChannels = 1,
                                                                int sampleFormat = SAMPLE_FMT_S16,
                                                                int outSampleFormat = SAMPLE_FMT_NONE,
                                                                int outChannels = 0)
    {
        return new StreamResampler(highQuality, factor, inputSampleRate, outputSampleRate, numChannels, sampleFormat, outSampleFormat, outChannels);
    }


    /// <summary>
    /// 
    /// Interface used to work with float type inputs and outputs.
    /// 
    /// </summary>
    public interface IFloatPacketResampler : IStreamResamplerInfo, IDisposable
    {
        unsafe long Process(bool lastPacket, float* input, long inputSize, float* output, long outputSize);

        long Process(bool lastPacket, float[] input, long inputSize, float[] output, long outputSize);
    }


    /// <summary>
    /// 
    /// Create instance of resampler and return its <see cref="IFloatPacketResampler"/> interface.
    /// 
    /// <para>See summary for <see cref="StreamResampler"/> class for description of parameters.</para>
    /// </summary>
    public static IFloatPacketResampler NewFloatPacketResampler(bool highQuality,
                                                                float factor,
                                                                int inputSampleRate = 0,
                                                                int outputSampleRate = 0,
                                                                int numChannels = 1,
                                                                int sampleFormat = SAMPLE_FMT_S16,
                                                                int outSampleFormat = SAMPLE_FMT_NONE,
                                                                int outChannels = 0)
    {
        return new StreamResampler(highQuality, factor, inputSampleRate, outputSampleRate, numChannels, sampleFormat, outSampleFormat, outChannels);
    }


    /// <summary>
    /// 
    /// As per function name, calculate expected output size for given input size and resampling factor.
    /// 
    /// </summary>
    /// <param name="inputSize"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static long GetExpectedOutputSize(long inputSize, float factor)
    {
        return (long)((double)inputSize * factor) + 400;
    }


    /// <summary>
    /// 
    /// This is used for validation of some of parameters used to create a resampler. Values that need to be provided are:
    /// 
    /// <list type="bullet">
    ///     <item>only <paramref name="factor"/></item>
    ///     <item>only <paramref name="inputSampleRate"/> and <paramref name="outputSampleRate"/></item>
    ///     <item>or <paramref name="factor"/> and only one of sample rates</item>
    /// </list>
    /// </summary>
    /// <param name="factor"></param>
    /// <param name="inputSampleRate"></param>
    /// <param name="outputSampleRate"></param>
    /// <returns>Calculated and validated value for <paramref name="factor"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static float CalcFactor(float factor = 0.0f, int inputSampleRate = 0, int outputSampleRate = 0)
    {
        if (factor > 0.0f && inputSampleRate > 0 && outputSampleRate > 0)
        {
            throw new ArgumentException("Provide only factor, only input and output sample rates, or only factor and one of sample rates.");
        }

        if (factor <= 0.0f)
        {
            if (inputSampleRate == 0 || outputSampleRate == 0)
            {
                throw new ArgumentException("If factor is unspecified, both input and output sample rates must be provided.");
            }

            factor = (float)outputSampleRate / (float)inputSampleRate;
        }

        return factor;
    }


    /// <summary>
    /// 
    /// This is used for validation of some of parameters used to create a resampler. Values that need to be provided are:
    /// 
    /// <list type="bullet">
    ///     <item>only <paramref name="factor"/></item>
    ///     <item>only <paramref name="inputSampleRate"/> and <paramref name="outputSampleRate"/></item>
    ///     <item>or <paramref name="factor"/> and only one of sample rates</item>
    /// </list>
    /// </summary>
    /// <param name="factor"></param>
    /// <param name="inputSampleRate"></param>
    /// <param name="outputSampleRate"></param>
    /// <returns>Calculated and validated value for <paramref name="inputSampleRate"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static int CalcInputSampleRate(float factor, int inputSampleRate, int outputSampleRate)
    {
        if (factor > 0.0f && inputSampleRate > 0 && outputSampleRate > 0)
        {
            throw new ArgumentException("Provide only factor, only input and output sample rates, or only factor and one of sample rates.");
        }

        if (inputSampleRate <= 0)
        {
            if (factor <= 0.0f || outputSampleRate <= 0)
            {
                throw new ArgumentException("If input sample rate is unspecified, factor or both factor and output sample rate must be provided.");
            }

            inputSampleRate = (int)((float)outputSampleRate / factor);
        }

        return inputSampleRate;
    }


    /// <summary>
    /// 
    /// This is used for validation of some of parameters used to create a resampler. Values that need to be provided are:
    /// 
    /// <list type="bullet">
    ///     <item>only <paramref name="factor"/></item>
    ///     <item>only <paramref name="inputSampleRate"/> and <paramref name="outputSampleRate"/></item>
    ///     <item>or <paramref name="factor"/> and only one of sample rates</item>
    /// </list>
    /// </summary>
    /// <param name="factor"></param>
    /// <param name="inputSampleRate"></param>
    /// <param name="outputSampleRate"></param>
    /// <returns>Calculated and validated value for <paramref name="outputSampleRate"/>.</returns>
    /// <exception cref="ArgumentException"></exception>
    public static int CalcOutputSampleRate(float factor, int inputSampleRate, int outputSampleRate)
    {
        if (factor > 0.0f && inputSampleRate > 0 && outputSampleRate > 0)
        {
            throw new ArgumentException("Provide only factor, only input and output sample rates, or only factor and one of sample rates.");
        }

        if (outputSampleRate <= 0)
        {
            if (factor <= 0.0f || inputSampleRate <= 0)
            {
                throw new ArgumentException("If output sample rate is unspecified, factor or both factor and input sample rate must be provided.");
            }

            outputSampleRate = (int)((float)inputSampleRate * factor);
        }

        return outputSampleRate;
    }

    private static unsafe byte* ShortArrToPtrUnsafe(short[] arr)
    {
        fixed (short* ptr = arr) { return (byte*)ptr; }
    }

    private static unsafe byte* ByteArrToPtrUnsafe(byte[] arr)
    {
        fixed (byte* ptr = arr) { return ptr; }
    }


    /// <summary>
    /// Value used by Apple (Core Audio)1, ALSA2, MatLab2, sndlib2.
    /// <para>[1] Link: <a href="https://web.archive.org/web/20210210064026/http://blog.bjornroche.com/2009/12/int-float-int-its-jungle-out-there.html">Int->Float->Int: It's a jungle out there! (Web Archive)</a></para>
    /// <para>[2] Link: <a href="http://www.mega-nerd.com/libsndfile/FAQ.html#Q010">Q10: Reading a 16 bit PCM file as normalised floats...</a></para>
    /// </summary>
    private const float CONVERT_FACTOR_SHORT = 32768.0f;


    /// <summary>
    /// Every audio channel has its own indenpendent resampler. It is a wrapper around instance of <see cref="ReSampler"/> and
    /// provides it with an interface for on-the-fly conversion.
    /// </summary>
    private unsafe class ChannelResampler 
        : ReSampler.IInputProducer
        , ReSampler.IOutputConsumer
        , IDisposable
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

        internal ChannelResampler(bool highQuality, float factor, int ioff, int istep)
        {
            _resampler = new(highQuality, factor, factor);
            _ioff = ioff;
            _istep = istep;
        }

        internal unsafe long ProcessInput(float factor, bool lastPacket, byte* input, long inputSize, byte* output, long outputSize)
        {
            _input = (short *)input;
            _output = (short *)output;
            _inputSamplesUsed = 0;
            _inputSampleCount = inputSize / sizeof(short);
            _outputSampleCount = 0;
            _outputSampleMaxCount = outputSize / sizeof(short);

            _resampler.Process(factor, this, this, lastPacket);
            if (_inputSamplesUsed < _inputSampleCount)
            {
                throw new InvalidOperationException("Previous operation did not process all of input.");
            }

            return _outputSampleCount * sizeof(short);
        }

        /// <summary> See summary for <see cref="ReSampler.IInputProducer.GetInputBufferLenght"/> </summary>
        public int GetInputBufferLenght()
        {
            return (int)(_inputSampleCount - _inputSamplesUsed);
        }

        /// <summary> See summary for <see cref="ReSampler.IOutputConsumer.GetOutputBufferLength"/> </summary>
        public int GetOutputBufferLength()
        {
            return (int)(_outputSampleMaxCount - _outputSampleCount);
        }

        /// <summary> See summary for <see cref="ReSampler.IInputProducer.ProduceInput"/> </summary>
        public void ProduceInput(float[] array, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                int index = (int)(_ioff + ((_inputSamplesUsed + i) * _istep));
                array[offset + i] = _input[index] / CONVERT_FACTOR_SHORT;
            }
            _inputSamplesUsed += length;
        }

        /// <summary> See summary for <see cref="ReSampler.IOutputConsumer.ConsumeOutput"/> </summary>
        public void ConsumeOutput(float[] array, int offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                int index = (int)(_ioff + ((_outputSampleCount + i) * _istep));
                _output[index] = (short)(array[offset + i] * CONVERT_FACTOR_SHORT);
            }
            _outputSampleCount += length;
        }

        public void Dispose()
        {
        }
    }

    public bool HighQuality { get { return _highQuality; } }

    public float Factor { get { return _factor; } }

    public int InputSampleRate { get { return _inputSampleRate; } }

    public int OutputSampleRate { get { return _outputSampleRate; } }

    public int NumChannels { get { return _numChannels; } }

    public int SampleFormat { get { return _sampleFormat; } }

    public int OutputSampleFormat { get { return _outSampleFormat; } }

    public int OutputChannels { get { return _outChannels; } }

    public long InPacketCount { get { return _inPacketCount; } }

    public long InBytesProcessed { get { return _inBytesProcessed; } }

    public long OutBytesGenerated { get { return _outBytesGenerated; } }

    private bool _highQuality;

    private float _factor;

    private int _inputSampleRate;

    private int _outputSampleRate;

    private int _numChannels;

    private int _sampleFormat;

    private int _outSampleFormat;

    private int _outChannels;

    private ChannelResampler[] _chrs;

    private long _outSamplesReady = 0;

    private long _inPacketCount = 0;

    private long _inBytesProcessed = 0;

    private long _outBytesGenerated = 0;

    private StreamResampler(bool highQuality,
                            float factor,
                            int inputSampleRate = 0,
                            int outputSampleRate = 0,
                            int numChannels = 1,
                            int sampleFormat = SAMPLE_FMT_S16,
                            int outSampleFormat = SAMPLE_FMT_NONE,
                            int outChannels = 0)
    {
        if (numChannels < 1 || numChannels > 2 || outChannels < 0 || outChannels > 2)
        {
            throw new ArgumentException("Unsupported number of channels.");
        }

        if (sampleFormat != SAMPLE_FMT_S16)
        {
            throw new ArgumentException("Unsupported sample format.");
        }

        _highQuality = highQuality;
        _factor = CalcFactor(factor, inputSampleRate, outputSampleRate);
        _inputSampleRate = CalcInputSampleRate(factor, inputSampleRate, outputSampleRate);
        _outputSampleRate = CalcOutputSampleRate(factor, inputSampleRate, outputSampleRate);
        _numChannels = numChannels;
        _sampleFormat = sampleFormat;
        _outSampleFormat = outSampleFormat;
        _outChannels = outChannels;

        _chrs = new ChannelResampler[numChannels];
        if (numChannels == 1)
        {
            _chrs[0] = new ChannelResampler(_highQuality, _factor, 0, 1);
        }
        else if (numChannels == 2)
        {
            _chrs[0] = new ChannelResampler(_highQuality, _factor, 0, 2);
            _chrs[1] = new ChannelResampler(_highQuality, _factor, 1, 2);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _numChannels; i++)
        {
            _chrs[i].Dispose();
        }
        _numChannels = 0;
    }

    public unsafe long Process(bool lastPacket, byte* input, long inputSize, byte* output, long outputSize)
    {
        long outputUsed = 0;
        for (int i=0; i < _numChannels; i++)
        {
            // TODO(?): Assert that all channels returned the same value.
            outputUsed += _chrs[i].ProcessInput(_factor, lastPacket, input, inputSize / _numChannels, output, outputSize / _numChannels);
        }

        _inPacketCount++;
        _inBytesProcessed += inputSize;
        _outBytesGenerated += outputSize;
        return outputUsed;
    }

    public long Process(bool lastPacket, byte[] input, long inputSize, byte[] output, long outputSize)
    {
        unsafe
        {
            fixed (byte* inputPtr = input, outputPtr = output)
            {
                return Process(lastPacket, inputPtr, inputSize, outputPtr, outputSize);
            }
        }
    }

    public long ProcessToShort(bool lastPacket, byte[] input, long inputSize, ref short[]? output)
    {
        long outputSize = GetExpectedOutputSize(inputSize, _factor) / sizeof(short);
        if (output is null || output.Length < outputSize)
        {
            output = new short[outputSize];
        }

        unsafe
        {
            return Process(lastPacket, ByteArrToPtrUnsafe(input), inputSize, ShortArrToPtrUnsafe(output), outputSize * sizeof(short)) / sizeof(short);
        }
    }

    public unsafe long Process(bool lastPacket, short* input, long inputSize, short* output, long outputSize)
    {
        throw new NotImplementedException();
    }

    public long Process(bool lastPacket, short[] input, long inputSize, short[] output, long outputSize)
    {
        throw new NotImplementedException();
    }

    public long ProcessToByte(bool lastPacket, short[] input, long inputSize, ref byte[]? output)
    {
        long inByteSize = inputSize * sizeof(short);
        long outputSize = GetExpectedOutputSize(inByteSize, _factor);
        if (output is null || output.Length < outputSize)
        {
            output = new byte[outputSize];
        }

        unsafe
        {
            return Process(lastPacket, ShortArrToPtrUnsafe(input), inByteSize, ByteArrToPtrUnsafe(output), outputSize);
        }
    }

    public unsafe long Process(bool lastPacket, float* input, long inputSize, float* output, long outputSize)
    {
        throw new NotImplementedException();
    }

    public long Process(bool lastPacket, float[] input, long inputSize, float[] output, long outputSize)
    {
        throw new NotImplementedException();
    }
}
