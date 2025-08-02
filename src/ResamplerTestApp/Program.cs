using FFmpeg.AutoGen;
using LibStreamResampler;
using MFFAmpeg;
using MFFAmpeg.AVFormats;
using MFFAmpeg.Base;
using System.Diagnostics;


namespace ResamplerTestApp;

internal class Program
{
    private const string OUTPUT_FILE = "resampler_test.wav";

    private const int OUTPUT_RATE = 44100;


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


        RunResampler(args[0], OUTPUT_FILE, OUTPUT_RATE);
    }

    private static void RunResampler(string inputPath, string outputPath, int outputSampleRate)
    {
        Console.WriteLine($" Input path: {inputPath}");
        Console.WriteLine($"Output path: {outputPath}");

        var reader =
            MFFApi.OpenAudioReader(inputPath, MFFApi.INPUT_FORMAT_WAV)
            .ThrowIfError();

        var fileFormat = reader.FileFormat;
        var inputFormat = reader.StreamFormat;
        ThrowIfUnsupported(fileFormat, inputFormat);

        var inputStream = 
            reader.OpenMainStream()
            .ThrowIfError();

        Console.WriteLine("Loading input into memory...");
        var packetList =
            MFFApi.CreatePacketList(inputStream);

        var outputFormat =
            new MAudioStreamFormat(outputSampleRate, inputFormat._bits_per_sample, inputFormat._num_channels);

        var resampler = StreamResampler.Create(
            true, inputFormat._num_channels, inputFormat._sample_rate, outputFormat._sample_rate);

        Console.WriteLine($"Sample ratio factor: {resampler.Factor}");

        Console.WriteLine("Pre-allocating output buffer...");
        var outputData = new List<MByteBuffer>(packetList.Count);
        foreach (var packet in packetList)
        {
            outputData.Add(MFFApi.AllocPacketBuffer((ulong)
                StreamResampler.GetExpectedOutputSize((int)packet.Size, resampler.Factor))
            );
        }

        Console.WriteLine($"Input:");
        Console.WriteLine($"  Sample format: {fileFormat.CodecName()}");
        Console.WriteLine($"    Sample rate: {inputFormat._sample_rate}");
        Console.WriteLine($"Bits per sample: {inputFormat._bits_per_sample}");
        Console.WriteLine($"       Channels: {inputFormat._num_channels}");

        Console.WriteLine($"Output:");
        Console.WriteLine($"    Sample rate: {outputFormat._sample_rate}");

        Console.WriteLine("Creating output file...");

        var writer =
            MFFApi.OpenAudioWriter(outputPath, MFFApi.DEFAULT_AUDIO_FILE_FORMAT)
            .ThrowIfError();

        writer.AddStream(outputFormat)
            .ThrowIfError();

        var outputStream = 
            writer.StartPacketWriter()
            .ThrowIfError();

        Console.WriteLine("Running resampler...");

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        for (int i=0; i<packetList.Count; i++)
        {
            var packet = packetList[i];
            var buffer = outputData[i];
            unsafe
            {
                resampler.Process(packet.Data, packet.Size, packet.LastPacket);
                buffer.BytesUsed = (ulong)resampler.ConvertAndFillOutput(buffer.Data);
            }
        }
        stopwatch.Stop();
        var elapsed = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"Miliseconds elapsed: {elapsed}");

        Console.WriteLine("Saving to file...");

        long dataBytesWritten = 0;
        foreach (var buffer in outputData)
        {
            dataBytesWritten += (long)buffer.BytesUsed;
            outputStream.WritePacketFromData(buffer).ThrowIfError();
        }
        Console.WriteLine($"Bytes written: {dataBytesWritten}");

        writer.Dispose();
        reader.Dispose();

        var byteRateIn = (resampler.InBytesProcessed * 1000L) / elapsed;
        var byteRateOut = (resampler.OutBytesGenerated * 1000L) / elapsed;
        Console.WriteLine($"Bytes per sec:");
        Console.WriteLine($"   Input: {byteRateIn}");
        Console.WriteLine($"  Output: {byteRateOut}");
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
