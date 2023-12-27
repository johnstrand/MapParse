using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using System.Text.Json;

var tokens = Tokenizer.Create(File.ReadAllText("/home/john/Downloads/tb_tutch_01/maps/tb_tutch_01.map"));
var tokens2 = Tokenizer.Create(@"{
""spawnflags"" ""0""
""classname"" ""worldspawn""
""wad"" ""E:\q1maps\Q.wad""
{
( 256 64 16 ) ( 256 64 0 ) ( 256 0 16 ) mmetal1_2 0 0 0 1 1
( 0 0 0 ) ( 0 64 0 ) ( 0 0 16 ) mmetal1_2 0 0 0 1 1
( 64 256 16 ) ( 0 256 16 ) ( 64 256 0 ) mmetal1_2 0 0 0 1 1
( 0 0 0 ) ( 0 0 16 ) ( 64 0 0 ) mmetal1_2 0 0 0 1 1
( 64 64 0 ) ( 64 0 0 ) ( 0 64 0 ) mmetal1_2 0 0 0 1 1
( 0 0 -64 ) ( 64 0 -64 ) ( 0 64 -64 ) mmetal1_2 0 0 0 1 1
}
}
{
""spawnflags"" ""0""
""classname"" ""info_player_start""
""origin"" ""32 32 24""
}");

while (tokens2.HasMoreTokens)
{
    var e = ReadEntity(tokens2);
}

Entity ReadEntity(Tokenizer t)
{
    t.Expect("{");
    var properties = new Dictionary<string, string>();
    while (t.Peek() is not "{" and not "}")
    {
        var (key, value) = ReadProperty(t);
        properties[key] = value;
    }

    var brushes = new List<Brush>();
    while (t.Peek() == "{")
    {
        brushes.Add(ReadBrush(t));
    }

    t.Expect("}");

    return brushes.Count > 0 ? new BrushEntity(properties, brushes) : new PointEntity(properties);
}

Brush ReadBrush(Tokenizer t)
{
    var brush = new Brush();
    t.Expect("{");
    while (t.Peek() != "}")
    {
        brush.Point0 = ReadVector(t);
        brush.Point1 = ReadVector(t);
        brush.Point2 = ReadVector(t);
        brush.TextureName = t.Read();
        brush.OffsetX = float.Parse(t.Read());
        brush.OffsetY = float.Parse(t.Read());
        brush.Rotation = float.Parse(t.Read());
        brush.ScaleX = float.Parse(t.Read());
        brush.ScaleY = float.Parse(t.Read());
    }
    t.Expect("}");
    return brush;
}

Vector3 ReadVector(Tokenizer t)
{
    t.Expect("(");
    var x = float.Parse(t.Read());
    var y = float.Parse(t.Read());
    var z = float.Parse(t.Read());
    t.Expect(")");

    // Console.WriteLine($"\t({x}, {y}, {z})");
    return new Vector3(x, y, z);
}

(string key, string value) ReadProperty(Tokenizer t)
{
    var key = t.Read();
    var value = t.Read();
    return (key, value);
}

readonly struct Color(byte r, byte g, byte b, byte a = 255)
{
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;
    public byte A { get; } = a;

    public static Color Parse(string value)
    {
        var parts = value.Split(' ');
        return new Color(byte.Parse(parts[0]), byte.Parse(parts[1]), byte.Parse(parts[2]));
    }
}

struct Brush
{
    public Vector3 Point0;
    public Vector3 Point1;
    public Vector3 Point2;
    public string TextureName;
    public float OffsetX;
    public float OffsetY;
    public float Rotation;
    public float ScaleX;
    public float ScaleY;

    public readonly Plane ToPlane()
    {
        var v0 = Point1 - Point0;
        var v1 = Point2 - Point0;
        var normal = Vector3.Normalize(Vector3.Cross(v0, v1));
        var d = Vector3.Dot(normal, Point0);
        return new Plane(normal, d);
    }

    public static bool GetIntersection(Plane p0, Plane p1, Plane p2, out Vector3 intersection)
    {
        var v0 = p1.Normal;
        var v1 = p2.Normal;
        var normal = Vector3.Normalize(Vector3.Cross(v0, v1));
        var d = Vector3.Dot(normal, p0.Normal);
        if (Math.Abs(d) < 0.0001f)
        {
            intersection = Vector3.Zero;
            return false;
        }

        var u = Vector3.Cross(p0.Normal, p1.Normal);
        var v = Vector3.Cross(p2.Normal, p0.Normal);
        var w = Vector3.Cross(p1.Normal, p2.Normal);
        intersection = (u * p2.D + v * p0.D + w * p1.D) / d;
        return true;
    }
}

enum EntityType
{
    Point,
    Brush
}

abstract class Entity
{
    public abstract EntityType Type { get; }
    public string ClassName => GetValueOrDefault("classname", string.Empty);
    public IEnumerable<string> PropertyNames => properties.Keys;
    private readonly Dictionary<string, string> properties = [];

    public string this[string key]
    {
        get => GetValueOrDefault(key, string.Empty);
        set => properties[key] = value;
    }

    protected Entity(IEnumerable<KeyValuePair<string, string>> properties)
    {
        foreach (var (key, value) in properties)
        {
            this.properties[key] = value;
        }
    }

    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        return properties.TryGetValue(key, out value);
    }

    public bool IsSet(string key)
    {
        return properties.ContainsKey(key);
    }

    protected bool TryGetValue<T>(string key, Func<string, T> parser, [NotNullWhen(true)] out T? value)
    {
        if (properties.TryGetValue(key, out var stringValue))
        {
            try
            {
                value = parser(stringValue)!;
                return true;
            }
            finally { }
        }

        value = default;
        return false;
    }
    protected string GetValueOrDefault(string key, string defaultValue)
    {
        return properties.TryGetValue(key, out var value) ? value : defaultValue;
    }

    protected static Vector4 ParseVec4(string value)
    {
        var parts = value.Split(' ');
        return new Vector4(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
    }

    protected static Vector3 ParseVec3(string value)
    {
        var parts = value.Split(' ');
        return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
    }
}

class BrushEntity : Entity
{
    public override EntityType Type => EntityType.Brush;
    public IReadOnlyList<Brush> Brushes => brushes;
    private readonly List<Brush> brushes;
    public BrushEntity(IEnumerable<KeyValuePair<string, string>> properties, IEnumerable<Brush> brushes) : base(properties)
    {
        this.brushes = new(brushes);
    }
}

readonly struct Face
{
    public readonly Vector3[] Vertices;
    public readonly Vector3 Normal;

    public Face(Vector3[] vertices, Vector3 normal)
    {
        Vertices = vertices;
        Normal = normal;
    }

    public static IEnumerable<Face> FromBrushes(List<Brush> brushes)
    {
        for(var i = 0; i < brushes.Count - 2; i++)
        {
            for(var j = i + 1; j < brushes.Count - 1; j++)
            {
                for(var k = j + 1; k < brushes.Count; k++)
                {
                    if (Brush.GetIntersection(brushes[i].ToPlane(), brushes[j].ToPlane(), brushes[k].ToPlane(), out var intersection))
                    {
                    }
                }
            }
        }
    }
}

readonly struct Vertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Normal;
}

class PointEntity(IEnumerable<KeyValuePair<string, string>> properties) : Entity(properties)
{
    public override EntityType Type => EntityType.Point;
    public Vector3 Origin => ParseVec3(GetValueOrDefault("origin", "0 0 0"));
    public float Angle => float.Parse(GetValueOrDefault("angle", "0"));
    public int SpawnFlags => int.Parse(GetValueOrDefault("spawnflags", "0"));
}

class Tokenizer
{
    private readonly Queue<string> tokens;

    public bool HasMoreTokens => tokens.Count > 0;

    private Tokenizer(IEnumerable<string> tokens)
    {
        this.tokens = new(tokens);
    }

    public string Read()
    {
        EnsureToken();
        return tokens.Dequeue();
    }

    public string Peek()
    {
        EnsureToken();
        return tokens.Peek();
    }

    public bool TryRead(out string token)
    {
        if (tokens.Count > 0)
        {
            token = tokens.Dequeue();
            return true;
        }
        token = string.Empty;
        return false;
    }

    public void Expect(string token)
    {
        if (!TryRead(out var actual))
        {
            throw new Exception($"Expected {token} but found end of file");
        }

        if (actual != token)
        {
            throw new Exception($"Expected {token} but found {actual}");
        }
    }

    private void EnsureToken()
    {
        if (tokens.Count == 0)
        {
            throw new Exception("Unexpected end of file");
        }
    }

    public static Tokenizer Create(string input)
    {
        var buffer = new StringBuilder();
        var content = new Queue<char>(input);

        string Flush()
        {
            var result = buffer!.ToString();
            buffer.Clear();
            return result;
        }

        IEnumerable<string> TokenizeInner()
        {
            while (content.Count > 0)
            {
                var c = content.Dequeue();

                if (char.IsWhiteSpace(c))
                {
                    yield return Flush();
                }
                else if (c is '(' or ')' or '{' or '}')
                {
                    yield return Flush();
                    yield return c.ToString();
                }
                else if (c is '/' && content.Count > 0 && content.Peek() == '/')
                {
                    yield return Flush();

                    while (content.Count > 0 && content.Peek() is not '\n' or '\r')
                    {
                        content.Dequeue();
                    }

                    var newline = content.Dequeue();
                    if (newline == '\r' && content.Count > 0 && content.Peek() == '\n')
                    {
                        content.Dequeue();
                    }
                }
                else if (c == '"')
                {
                    while (content.Count > 0)
                    {
                        c = content.Dequeue();
                        if (c == '"')
                        {
                            yield return Flush();
                            break;
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                    }

                    if (content.Count == 0 && buffer.Length > 0)
                    {
                        throw new Exception("Unterminated string");
                    }
                }
                else
                {
                    buffer.Append(c);
                }
            }
        }

        return new(TokenizeInner().Where(token => !string.IsNullOrWhiteSpace(token)));
    }
}
