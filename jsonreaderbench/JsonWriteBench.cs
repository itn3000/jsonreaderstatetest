using BenchmarkDotNet.Attributes;
using System.Text.Json;
using System.IO;

[MemoryDiagnoser]
[ShortRunJob]
public class JsonWriterBench
{
    [Params(10, 10000)]
    public int Num;
    [Benchmark]
    public void HighMemory()
    {
        using var fstm = Stream.Null;
        using var jw = new Utf8JsonWriter(fstm, new JsonWriterOptions() { Indented = true });
        JsonSerializer.Serialize(jw, Create(Num));
        static IEnumerable<Hoge> Create(int num)
        {
            for (int i = 0; i < num; i++)
            {
                yield return new Hoge() { A = $"hoge{i}", B = i };
            }
        }

    }
    [Benchmark]
    public void LowMemory()
    {
        using var fstm = Stream.Null;
        using var jw = new Utf8JsonWriter(fstm, new JsonWriterOptions() { Indented = true });
        jw.WriteStartArray();
        for (int i = 0; i < Num; i++)
        {
            JsonSerializer.Serialize(jw, new Hoge() { A = $"hoge{i}", B = i });
        }
        jw.WriteEndArray();
    }
}

class Hoge
{
    public string A { get; set; }
    public int B { get; set; }
}