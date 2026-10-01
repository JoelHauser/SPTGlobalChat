using System.Globalization;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using SPTGlobalChat;

namespace SPTGlobalChat.Tests;

public class TextSafetyTests
{
    // The relay's tests read the same file, so the two cleaners cannot drift apart.
    static JsonElement[] Cases() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "clean-cases.json")))
            .RootElement.EnumerateArray().ToArray();

    public static IEnumerable<object[]> CaseNames() =>
        Cases().Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void MatchesTheRelay(string name)
    {
        var c = Cases().Single(x => x.GetProperty("name").GetString() == name);
        var input = c.GetProperty("in");
        object value = input.ValueKind == JsonValueKind.String ? Decode(input.GetRawText()) : 42L;
        Assert.Equal(Decode(c.GetProperty("out").GetRawText()), TextSafety.Clean(value));
    }

    [Fact]
    public void CapsAtMaxTextCodePoints()
    {
        var output = TextSafety.Clean(new string('a', 1000));
        Assert.Equal(TextSafety.MaxText, output.Length);
    }

    [Fact]
    public void NeverSplitsASurrogatePairAtTheCap()
    {
        var emoji = char.ConvertFromUtf32(0x1F600);
        var output = TextSafety.Clean(string.Concat(Enumerable.Repeat(emoji, 400)));
        Assert.Equal(string.Concat(Enumerable.Repeat(emoji, TextSafety.MaxText)), output);
    }

    [Fact]
    public void ALoneSurrogateDoesNotThrow()
    {
        // string.Normalize throws on one.
        Assert.Equal("ab", TextSafety.Clean("a\ud800b"));
    }

    [Fact]
    public void TheGamesParserNeverDeliversALoneSurrogate()
    {
        // Pins why the guard above is belt and braces on the relay path: Newtonsoft (the
        // game's parser) swaps an unpaired surrogate escape for U+FFFD while reading.
        var parsed = JsonConvert.DeserializeObject<string>("\"a\\ud800b\"");
        Assert.Equal("a\ufffdb", parsed);
    }

    // A JSON string literal, quotes included, to the exact UTF-16 it encodes -- lone
    // surrogates kept. Both System.Text.Json and Newtonsoft refuse or replace those,
    // and two of the shared cases exist to send one.
    static string Decode(string literal)
    {
        var s = literal.Substring(1, literal.Length - 2);
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\') { sb.Append(s[i]); continue; }
            var e = s[++i];
            switch (e)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber));
                    i += 4;
                    break;
                default: sb.Append(e); break;   // \" \\ \/
            }
        }
        return sb.ToString();
    }
}
