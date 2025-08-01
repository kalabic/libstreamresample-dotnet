using FFmpeg.AutoGen;
using LibStreamResampler;
using MFFAmpeg;
using MFFAmpeg.AVFormats;
using MFFAmpeg.Base;

namespace ResamplerTestApp;

internal class Program
{
    private const string OUTPUT_FILE = "resampler_test.wav";

    private const int OUTPUT_RATE = 6000;


    /// <summary>
    /// 
    /// The Main
    /// 
    /// </summary>
    /// <param name="args">Input audio file path.</param>
    private static void Main(string[] args)
    {
        if (MFFApi.Register() < 0)
        {
            Console.WriteLine($"FFmpeg shared libraries not found. Cannot continue.");
            return;
        }

        if (args.Length != 1)
        {
            Console.WriteLine($"A single argument is required, path to input audio file.");
            return;
        }
        Console.WriteLine($"Input file path: {args[0]}");


        RunResampler(args[0], OUTPUT_FILE, OUTPUT_RATE);
    }

    private static void RunResampler(string inputPath, string outputPath, int outputSampleRate)
    {
        IMAudioReader reader =
            MFFApi.OpenAudioReader(inputPath, MFFApi.INPUT_FORMAT_WAV).ThrowIfError();

        MAudioFileFormat fileFormat = reader.FileFormat;
        MAudioStreamFormat inputFormat = reader.StreamFormat;
        ThrowIfUnsupported(fileFormat, inputFormat);

        MAudioStreamFormat outputFormat =
            new MAudioStreamFormat(outputSampleRate, inputFormat._bits_per_sample, inputFormat._num_channels);

        Console.WriteLine();
        Console.WriteLine($"Input:");
        Console.WriteLine($"  Sample format: {fileFormat.CodecName()}");
        Console.WriteLine($"    Sample rate: {inputFormat._sample_rate}");
        Console.WriteLine($"Bits per sample: {inputFormat._bits_per_sample}");
        Console.WriteLine($"       Channels: {inputFormat._num_channels}");
        Console.WriteLine();
        Console.WriteLine($"Output:");
        Console.WriteLine($"    Sample rate: {outputFormat._sample_rate}");
        Console.WriteLine();

        IMAudioWriter writer =
            MFFApi.OpenAudioWriter(outputPath, MFFApi.DEFAULT_AUDIO_FILE_FORMAT).ThrowIfError();

        writer.AddStream(outputFormat).ThrowIfError();

        IMPacketWriter outputStream = writer.StartPacketWriter().ThrowIfError();

        IMPacketReader inputStream = reader.OpenMainStream().ThrowIfError();

        var resampler = StreamResampler.Create(
            true, inputFormat._num_channels, inputFormat._sample_rate, outputFormat._sample_rate);

        long sampleCount = 0;
        long timestamp = 0;

        foreach (var packet in inputStream)
        {
            MByteBuffer? buffer = null;
            unsafe
            {
                resampler.Process(packet.Data, packet.Size, packet.LastPacket);
                buffer = outputStream.AllocPacketBuffer((ulong)resampler.GetOutputSizeInBytes());
                buffer.BytesUsed = (ulong)resampler.ConvertAndFillOutput(buffer.Data);
            }

            sampleCount = (long)outputFormat.ByteToSampleCount(buffer.BytesUsed);

            var outputPacket = new MPacket(buffer);
            outputPacket.StreamIndex = 0;
            outputPacket.DTS = timestamp;
            outputPacket.Duration = sampleCount;
            outputPacket.PTS = timestamp;

            outputStream.Write(outputPacket).ThrowIfError();
            timestamp += sampleCount;

            outputPacket.Dispose();
            buffer.Dispose();
            packet.Dispose();
        }

        writer.Dispose();
        reader.Dispose();

        Console.WriteLine();
        Console.WriteLine($"     Input packet count: {resampler.InPacketCount}");
        Console.WriteLine($"  Input bytes processed: {resampler.InBytesProcessed}");
        Console.WriteLine($" Output bytes generated: {resampler.OutBytesGenerated}");
        Console.WriteLine();
        Console.WriteLine($"Output file: {outputPath}");
    }

    private static void ThrowIfUnsupported(MAudioFileFormat fileFormat, MAudioStreamFormat streamFormat)
    {
        if (fileFormat._codec_id != AVCodecID.AV_CODEC_ID_PCM_S16LE)
        {
            throw new NotImplementedException($"Unsupported sample format.");
        }

        if (streamFormat._num_channels < 1 || streamFormat._num_channels > 2)
        {
            throw new NotImplementedException($"Unsupported channel count.");
        }
    }
}
