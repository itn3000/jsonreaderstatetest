using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;

// See https://aka.ms/new-console-template for more information
CreateJsonFile("abc.json", 100 * 10000);
GC.Collect();
Console.WriteLine($"GC.TotalMemory = {GC.GetTotalMemory(false)}");
await ReadJsonFile("abc.json");
Console.WriteLine($"GC.TotalMemory = {GC.GetTotalMemory(false)}");

static void CreateJsonFile(string filePath, int num)
{
    using var fstm = File.Create(filePath);
    using var jw = new Utf8JsonWriter(fstm, new JsonWriterOptions() { Indented = true });
    jw.WriteStartArray();
    for (int i = 0; i < num; i++)
    {
        JsonSerializer.Serialize(jw, new Hoge() { A = $"hoge{i}", B = i });
    }
    jw.WriteEndArray();
}

static void CreateJsonFile2(string filePath, int num)
{
    using var fstm = File.Create(filePath);
    using var jw = new Utf8JsonWriter(fstm, new JsonWriterOptions() { Indented = true });
    JsonSerializer.Serialize(jw, Create(num));
    static IEnumerable<Hoge> Create(int num)
    {
        for (int i = 0; i < num; i++)
        {
            yield return new Hoge() { A = $"hoge{i}", B = i };
        }
    }
}

async Task ReadJsonFile2(string filePath)
{
    using var fstm = File.OpenRead(filePath);
    var deserialized = JsonSerializer.Deserialize<IEnumerable<Hoge>>(fstm)!;
    Console.WriteLine($"deserialized type = {deserialized.GetType()}");
    foreach(var item in deserialized!)
    {
        if(item.B % 0xfff == 0)
        {
            Console.WriteLine($"{item.A}, {item.B}");
        }
    }
}

async Task ReadJsonFile(string filePath)
{
    using var fstm = File.OpenRead(filePath);
    var pipeReader = PipeReader.Create(fstm);
    await ReadTask(pipeReader);
    // var pipe = new Pipe();
    // await Task.WhenAll(WriteTask(pipe.Writer, fstm), ReadTask(pipe.Reader));
    // static async Task WriteTask(PipeWriter writer, Stream stm)
    // {
    //     using var wstm = writer.AsStream();
    //     await stm.CopyToAsync(wstm);
    //     await wstm.FlushAsync();
    // }
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
        if (readResult.Buffer.Length > 32)
        {
            Console.WriteLine($"first 16 chars({readResult.Buffer.Length}): {Encoding.UTF8.GetString(readResult.Buffer.Slice(0, 16))}");
            Console.WriteLine($"last 16 chars: {Encoding.UTF8.GetString(readResult.Buffer.Slice(readResult.Buffer.Length - 16))}");
        }
        else
        {
            Console.WriteLine($"{readResult.Buffer.Length}, {Encoding.UTF8.GetString(readResult.Buffer)}");
        }
        ReadJson(readResult, ref readerState, reader);
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
                            if (hoge.B % 0xffff == 0)
                            {
                                Console.WriteLine($"a={hoge.A}, b={hoge.B}");
                            }
                            totalRead = jr.BytesConsumed;
                            readerState = jr.CurrentState;
                        }
                    }
                    catch (JsonException je)
                    {
                        Console.WriteLine(je);
                        break;
                    }
                }
                else
                {
                    totalRead = jr.BytesConsumed;
                    readerState = jr.CurrentState;
                }
            }
            Console.WriteLine($"total read: {totalRead}");
            reader.AdvanceTo(readResult.Buffer.Slice(0, totalRead).End, readResult.Buffer.End);
        }
    }
}

class Hoge
{
    public string A { get; set; }
    public int B { get; set; }
}