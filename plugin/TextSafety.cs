using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SPTGlobalChat
{
    /// <summary>
    /// The plugin's copy of relay/src/clean.ts, run on everything before it is drawn --
    /// so a compromised or spoofed relay still cannot put a tag, a bidi override or a
    /// tower of combining marks on a player's screen. Pure: no Unity type, linked into
    /// the tests, which run it against the same docs/clean-cases.json as the relay.
    /// </summary>
    public static class TextSafety
    {
        public const int MaxText = 300;
        public const int MaxName = 32;

        // Combining marks allowed on one base character.
        const int MaxMarks = 2;

        // Written as regex escapes so this file stays plain ASCII. See clean.ts for what
        // each range is.
        static readonly Regex LoneSurrogate = new Regex(
            "[\\ud800-\\udbff](?![\\udc00-\\udfff])|(?<![\\ud800-\\udbff])[\\udc00-\\udfff]");

        static readonly Regex Breaks = new Regex("[\\t\\n\\v\\f\\r\\u0085\\u2028\\u2029]");

        static readonly Regex Unsafe = new Regex(
            "[\\u0000-\\u001f\\u007f-\\u009f\\u200b-\\u200f\\u2028-\\u202e\\u2060-\\u2069\\ufeff<>]");

        static readonly Regex Spaces = new Regex("\\s+");

        public static string Clean(object input, int max = MaxText)
        {
            if (!(input is string s)) return "";

            // Lone surrogates first: string.Normalize throws on them.
            s = LoneSurrogate.Replace(s, "");
            s = s.Normalize(NormalizationForm.FormC);
            s = Breaks.Replace(s, " ");
            s = Unsafe.Replace(s, "");

            var kept = new StringBuilder(s.Length);
            var marks = 0;
            for (var i = 0; i < s.Length; i += Width(s, i))
            {
                if (IsMark(CharUnicodeInfo.GetUnicodeCategory(s, i)))
                {
                    if (kept.Length == 0 || ++marks > MaxMarks) continue;
                }
                else
                {
                    marks = 0;
                }
                kept.Append(s, i, Width(s, i));
            }

            return Truncate(Spaces.Replace(kept.ToString(), " ").Trim(), max);
        }

        public static string CleanName(object input) => Clean(input, MaxName);

        static bool IsMark(UnicodeCategory c) =>
            c == UnicodeCategory.NonSpacingMark ||
            c == UnicodeCategory.SpacingCombiningMark ||
            c == UnicodeCategory.EnclosingMark;

        static int Width(string s, int i) => char.IsSurrogatePair(s, i) ? 2 : 1;

        // By code point, so a pair is never split at the cap.
        static string Truncate(string s, int max)
        {
            var points = 0;
            for (var i = 0; i < s.Length; i += Width(s, i))
            {
                if (++points > max) return s.Substring(0, i).TrimEnd();
            }
            return s;
        }
    }
}
