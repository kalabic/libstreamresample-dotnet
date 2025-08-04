using FFmpeg.AutoGen;
using LibStreamResampler;
using MFFAmpeg;
using MFFAmpeg.AVBuffers;
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

        var inputStream = 
            reader.OpenMainStream()
            .ThrowIfError();

        var inputFormat = inputStream.StreamFormat;
        ThrowIfUnsupported(inputFormat);

        Console.WriteLine("Loading input into memory...");
        var packetList =
            MFFApi.CreatePacketList(inputStream);

        var outputFormat =
            new MAudioStreamFormat(AVSampleFormat.AV_SAMPLE_FMT_S16, outputSampleRate, inputFormat.BitsPerSample, inputFormat.NumChannels);

        var resampler = StreamResampler.Create(
            true, inputFormat.NumChannels, inputFormat.SampleRate, outputFormat.SampleRate);

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
        Console.WriteLine($"  Sample format: {inputFormat.Format}");
        Console.WriteLine($"    Sample rate: {inputFormat.SampleRate}");
        Console.WriteLine($"Bits per sample: {inputFormat.BitsPerSample}");
        Console.WriteLine($"       Channels: {inputFormat.NumChannels}");

        Console.WriteLine($"Output:");
        Console.WriteLine($"    Sample rate: {outputFormat.SampleRate}");

        Console.WriteLine("Creating output file...");

        var writer =
            MFFApi.OpenAudioWriter(outputPath, MFFApi.FILE_FORMAT_WAV)
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

    private static void ThrowIfUnsupported(MAudioStreamFormat streamFormat)
    {
        if (streamFormat.Format != AVSampleFormat.AV_SAMPLE_FMT_S16)
        {
            throw new NotImplementedException($"Unsupported sample format.");
        }

        if (streamFormat.NumChannels < 1 || streamFormat.NumChannels > 2)
        {
            throw new NotImplementedException($"Unsupported channel count.");
        }
    }
}
