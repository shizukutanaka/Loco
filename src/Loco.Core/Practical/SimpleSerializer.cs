// Rob Pike: "Don't design with interfaces, discover them"
// John Carmack: "Prefer simple, direct solutions"

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace Loco.Core.Practical;

/// <summary>
/// Simple serialization - JSON, XML, Binary without complexity
/// Fast, type-safe, zero dependencies beyond BCL
/// </summary>
public static class SimpleSerializer
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    // JSON Serialization
    public static string ToJson<T>(T obj, bool pretty = false)
    {
        return JsonSerializer.Serialize(obj, pretty ? PrettyJsonOptions : DefaultJsonOptions);
    }

    public static T? FromJson<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, DefaultJsonOptions);
    }

    public static async Task<string> ToJsonAsync<T>(T obj, Stream stream, bool pretty = false)
    {
        await JsonSerializer.SerializeAsync(stream, obj, pretty ? PrettyJsonOptions : DefaultJsonOptions);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public static async Task<T?> FromJsonAsync<T>(Stream stream)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, DefaultJsonOptions);
    }

    // XML Serialization
    public static string ToXml<T>(T obj)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true });
        serializer.Serialize(xmlWriter, obj);
        return stringWriter.ToString();
    }

    public static T? FromXml<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stringReader = new StringReader(xml);
        return (T?)serializer.Deserialize(stringReader);
    }

    // Binary serialization (simple format)
    public static byte[] ToBinary<T>(T obj)
    {
        var json = ToJson(obj);
        return Encoding.UTF8.GetBytes(json);
    }

    public static T? FromBinary<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return FromJson<T>(json);
    }

    // Base64 encoding
    public static string ToBase64<T>(T obj)
    {
        var bytes = ToBinary(obj);
        return Convert.ToBase64String(bytes);
    }

    public static T? FromBase64<T>(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        return FromBinary<T>(bytes);
    }

    // Clone via serialization
    public static T? DeepClone<T>(T obj)
    {
        var json = ToJson(obj);
        return FromJson<T>(json);
    }
}

/// <summary>
/// Simple CSV serializer for tabular data
/// </summary>
public static class SimpleCsv
{
    public static string ToCsv<T>(IEnumerable<T> items, bool includeHeaders = true)
    {
        var sb = new StringBuilder();
        var properties = typeof(T).GetProperties();

        // Headers
        if (includeHeaders)
        {
            sb.AppendLine(string.Join(",", properties.Select(p => EscapeCsv(p.Name))));
        }

        // Data
        foreach (var item in items)
        {
            var values = properties.Select(p =>
            {
                var value = p.GetValue(item)?.ToString() ?? "";
                return EscapeCsv(value);
            });
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    public static List<Dictionary<string, string>> FromCsv(string csv)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return new List<Dictionary<string, string>>();

        var headers = ParseCsvLine(lines[0]);
        var result = new List<Dictionary<string, string>>();

        for (int i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, string>();

            for (int j = 0; j < Math.Min(headers.Length, values.Length); j++)
            {
                row[headers[j]] = values[j];
            }

            result.Add(row);
        }

        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (line[i] == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(line[i]);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}

/// <summary>
/// Simple binary writer/reader for custom formats
/// </summary>
public class SimpleBinaryFormat
{
    public static void Write(Stream stream, Action<BinaryWriter> write)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        write(writer);
    }

    public static T Read<T>(Stream stream, Func<BinaryReader, T> read)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return read(reader);
    }

    public static byte[] Pack(params object[] values)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        foreach (var value in values)
        {
            switch (value)
            {
                case bool b: writer.Write(b); break;
                case byte b: writer.Write(b); break;
                case int i: writer.Write(i); break;
                case long l: writer.Write(l); break;
                case float f: writer.Write(f); break;
                case double d: writer.Write(d); break;
                case string s: writer.Write(s); break;
                case byte[] bytes:
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                    break;
                default:
                    throw new ArgumentException($"Unsupported type: {value?.GetType()}");
            }
        }

        return ms.ToArray();
    }
}

/// <summary>
/// Simple data compression
/// </summary>
public static class SimpleCompression
{
    public static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    public static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public static string CompressString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var compressed = Compress(bytes);
        return Convert.ToBase64String(compressed);
    }

    public static string DecompressString(string compressed)
    {
        var bytes = Convert.FromBase64String(compressed);
        var decompressed = Decompress(bytes);
        return Encoding.UTF8.GetString(decompressed);
    }
}

/// <summary>
/// Example usage
/// </summary>
public class SerializationExamples
{
    public record Person(string Name, int Age, DateTime BirthDate);
    public record Order(string Id, List<OrderItem> Items, decimal Total);
    public record OrderItem(string Product, int Quantity, decimal Price);

    public static void Examples()
    {
        // JSON serialization
        var person = new Person("John", 30, new DateTime(1993, 5, 15));
        var json = SimpleSerializer.ToJson(person);
        var personBack = SimpleSerializer.FromJson<Person>(json);

        // XML serialization
        var xml = SimpleSerializer.ToXml(person);
        var personFromXml = SimpleSerializer.FromXml<Person>(xml);

        // Deep clone
        var clone = SimpleSerializer.DeepClone(person);

        // CSV serialization
        var people = new[]
        {
            new Person("Alice", 25, DateTime.Now.AddYears(-25)),
            new Person("Bob", 30, DateTime.Now.AddYears(-30)),
        };
        var csv = SimpleCsv.ToCsv(people);
        var dataBack = SimpleCsv.FromCsv(csv);

        // Binary packing
        var packed = SimpleBinaryFormat.Pack("Hello", 42, true, 3.14);

        // Compression
        var text = "This is a long text that needs compression...";
        var compressed = SimpleCompression.CompressString(text);
        var decompressed = SimpleCompression.DecompressString(compressed);
    }
}