using System.Text.Json;
using System.IO;
using BenchmarkDotNet.Attributes;
using System.IO.Pipelines;
using System.Text;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonReaderBench
{
    [Benchmark]
    public async Task DeserializeBulk()
    {
        using var mstm = new MemoryStream(Data);
        await JsonSerializer.DeserializeAsync<IEnumerable<Hoge>>(mstm);
    }
    [Benchmark]
    public async Task DeserializePartial()
    {
        using var mstm = new MemoryStream(Data);
        await ReadJsonFile(mstm);
    }
    byte[] Data = Array.Empty<byte>();
    [GlobalSetup]
    public void Setup()
    {
        const int num = 10000;
        using var stm = new MemoryStream(4096);
        JsonSerializer.Serialize(stm, Enumerable.Range(0, num).Select(i => new Hoge() { A = $"hoge{i}", B = i }).ToArray());
        Data = stm.ToArray();
    }
    async Task ReadJsonFile(Stream stm)
    {
        // var reader = PipeReader.Create(stm);
        // await ReadTask(reader);
        var pipe = new Pipe();
        await Task.WhenAll(WriteTask(pipe.Writer, stm), ReadTask(pipe.Reader));
        static async Task WriteTask(PipeWriter writer, Stream stm)
        {
            using var wstm = writer.AsStream();
            await stm.CopyToAsync(wstm);
            await wstm.FlushAsync();
        }
    }

    async Task ReadTask(PipeReader reader)
    {
        var readerState = new JsonReaderState(new JsonReaderOptions() { AllowMultipleValues = true });
        while (true)
        {
            var readResult = await reader.ReadAtLeastAsync(1024);
            if (readResult.Buffer.IsEmpty && readResult.IsCompleted)
            {
                break;
            }
            if(!readResult.Buffer.IsEmpty)
            {
                try
                {
                    ReadJson(readResult, ref readerState, reader);
                }
                catch(Exception e)
                {
                    throw new Exception($"failed: {readResult.Buffer.Length}", e);
                }
            }
            else
            {
                reader.AdvanceTo(readResult.Buffer.End);
            }
            static void ReadJson(ReadResult readResult, ref JsonReaderState readerState, PipeReader reader)
            {
                // ここはUtf8JsonReaderがref structなのでasyncメソッドの中では使えない
                var jr = new Utf8JsonReader(readResult.Buffer, false, readerState);
                long totalRead = 0;
                while (jr.Read())
                {
                    if (jr.TokenType == JsonTokenType.StartObject)
                    {
                        try
                        {
                            var hoge = JsonSerializer.Deserialize<Hoge>(ref jr);
                            if (hoge != null)
                            {
                                totalRead = jr.BytesConsumed;
                                readerState = jr.CurrentState;
                            }
                        }
                        catch (JsonException)
                        {
                            break;
                        }
                    }
                    else
                    {
                        totalRead = jr.BytesConsumed;
                        readerState = jr.CurrentState;
                    }
                }
                reader.AdvanceTo(readResult.Buffer.Slice(0, totalRead).End, readResult.Buffer.End);
            }
        }
    }
}