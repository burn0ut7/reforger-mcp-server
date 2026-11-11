using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

internal static class TelemetryBinaryTool
{
    public static int Run(string input, string output)
    {
        var files = File.Exists(input) ? new[] { Path.GetFullPath(input) } : Directory.GetFiles(input, "*.json");
        if (files.Length == 0) throw new InvalidDataException("No input captures.");
        Directory.CreateDirectory(output);
        var sources = new List<byte[]>();
        var documents = new List<JsonDocument>();
        var binaries = new List<byte[]>();
        var manifest = new List<object>();
        long gzipJson = 0, gzipBinary = 0, brotliJson = 0, brotliBinary = 0;
        try
        {
            foreach (var path in files.Order(StringComparer.Ordinal))
            {
                if (new FileInfo(path).Length > TelemetryContract.MaxBatchBytes) throw new InvalidDataException("Input too large.");
                var source = File.ReadAllBytes(path);
                var document = JsonDocument.Parse(source);
                documents.Add(document);
                sources.Add(source);
                var binary = TelemetryBinary.Encode(document.RootElement);
                var decoded = TelemetryBinary.Decode(binary);
                using var roundTrip = JsonDocument.Parse(decoded);
                if (!JsonElement.DeepEquals(document.RootElement, roundTrip.RootElement))
                    throw new InvalidDataException($"Binary round-trip changed logical data: {Path.GetFileName(path)}");
                binaries.Add(binary);
                var name = Path.GetFileNameWithoutExtension(path) + ".l2t";
                WriteNewOrIdentical(Path.Combine(output, name), binary);
                var brotli = Brotli(binary);
                WriteNewOrIdentical(Path.Combine(output, name + ".br"), brotli);
                gzipJson += Gzip(source).Length;
                gzipBinary += Gzip(binary).Length;
                brotliJson += Brotli(source).Length;
                brotliBinary += brotli.Length;
                manifest.Add(new { source = Path.GetFullPath(path), binary = name, rawBytes = source.Length, binaryBytes = binary.Length });
            }
            double Measure(Action action)
            {
                action();
                var samples = new List<double>();
                for (var i = 0; i < 7; i++)
                {
                    var clock = Stopwatch.StartNew();
                    action();
                    samples.Add(clock.Elapsed.TotalMilliseconds);
                }
                samples.Sort();
                return samples[3];
            }
            var compressedBinaries = binaries.Select(Brotli).ToArray();
            var timing = new
            {
                jsonValidateEncodeMs = Measure(() => { foreach (var d in documents) { TelemetryContract.Validate(d.RootElement); _ = JsonSerializer.SerializeToUtf8Bytes(d.RootElement); } }),
                binaryValidateEncodeMs = Measure(() => { foreach (var d in documents) _ = TelemetryBinary.Encode(d.RootElement); }),
                jsonParseValidateMs = Measure(() => { foreach (var b in sources) { using var d = JsonDocument.Parse(b); TelemetryContract.Validate(d.RootElement); } }),
                binaryDecodeValidateAndJsonProjectionMs = Measure(() => { foreach (var b in binaries) _ = TelemetryBinary.Decode(b); }),
                jsonBrotliMs = Measure(() => { foreach (var b in sources) _ = Brotli(b); }),
                binaryBrotliMs = Measure(() => { foreach (var b in binaries) _ = Brotli(b); }),
                binaryBrotliDecodeMs = Measure(() => { foreach (var b in compressedBinaries) { using var input = new MemoryStream(b); using var stream = new BrotliStream(input, CompressionMode.Decompress); stream.CopyTo(Stream.Null); } })
            };
            var report = new
            {
                chunks = files.Length, roundTripExactLogicalData = true,
                jsonBytes = sources.Sum(x => (long)x.Length), binaryBytes = binaries.Sum(x => (long)x.Length),
                gzipJsonBytes = gzipJson, gzipBinaryBytes = gzipBinary, brotliJsonBytes = brotliJson, brotliBinaryBytes = brotliBinary,
                timing, scope = "External .NET median of 7 warmed whole-corpus runs, no file IO; binary decoding includes JSONB projection. Compression is chunk-local Brotli quality 4/window 22 or gzip Fastest. Not game-thread or multiplayer evidence.",
                files = manifest
            };
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(output, "benchmark.json"), json);
            Console.WriteLine(json[..json.IndexOf("\"files\"", StringComparison.Ordinal)] + "\"files\":\"see benchmark.json\"}");
            return 0;
        }
        finally { foreach (var document in documents) document.Dispose(); }
    }

    private static void WriteNewOrIdentical(string path, byte[] bytes)
    {
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new IOException("Output would overwrite different data.");
            return;
        }
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        file.Write(bytes);
    }

    private static byte[] Brotli(byte[] bytes)
    {
        var output = new byte[BrotliEncoder.GetMaxCompressedLength(bytes.Length)];
        if (!BrotliEncoder.TryCompress(bytes, output, out var written, quality: 4, window: 22)) throw new InvalidDataException("Compression failed.");
        return output[..written];
    }

    private static byte[] Gzip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var compressor = new GZipStream(output, CompressionLevel.Fastest, true)) compressor.Write(bytes);
        return output.ToArray();
    }
}
