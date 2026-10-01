// What a chat line may not carry. Run on the relay before anything is stored or sent;
// the plugin runs its own copy (TextSafety.cs) before anything is drawn. Keep the two
// in step -- test/clean.test.ts and the plugin share the cases in docs/clean-cases.json.

export const MAX_TEXT = 300;
export const MAX_NAME = 32;

// Combining marks allowed on one base character. Real scripts need one or two;
// "Zalgo" text stacks dozens and draws over the lines around it.
const MAX_MARKS = 2;

// C0 and C1 controls, zero-width characters, line/paragraph separators, bidi embeds
// and overrides (U+202E turns "gnp.exe" into "exe.png" on screen), bidi isolates,
// word joiners, the BOM, and < > so no rich-text tag survives.
//
// Written as escaped strings so this file stays plain ASCII: an invisible bidi
// character in source is itself the hazard this code exists to remove.
const UNSAFE = new RegExp(
  "[\\u0000-\\u001f\\u007f-\\u009f\\u200b-\\u200f\\u2028-\\u202e\\u2060-\\u2069\\ufeff<>]",
  "g",
);

const BREAKS = new RegExp("[\\t\\n\\v\\f\\r\\u0085\\u2028\\u2029]", "g");

// Half of a surrogate pair with no other half. JSON can carry one ("\ud800"), and
// .NET's string.Normalize throws on it -- so it goes before anything else, on both ends.
const LONE_SURROGATE = new RegExp(
  "[\\ud800-\\udbff](?![\\udc00-\\udfff])|(?<![\\ud800-\\udbff])[\\udc00-\\udfff]",
  "g",
);

const MARK = /\p{M}/u;

export function cleanText(input: unknown, max = MAX_TEXT): string {
  if (typeof input !== "string") return "";
  const stripped = input
    .replace(LONE_SURROGATE, "")
    .normalize("NFC")
    .replace(BREAKS, " ")      // a line break is a space, not a join of two words
    .replace(UNSAFE, "");

  const out: string[] = [];
  let marks = 0;
  for (const ch of stripped) {               // by code point, so pairs never split
    if (MARK.test(ch)) {
      if (out.length === 0 || ++marks > MAX_MARKS) continue;
    } else {
      marks = 0;
    }
    out.push(ch);
  }

  return truncate(out.join("").replace(/\s+/g, " ").trim(), max);
}

export function cleanName(input: unknown): string {
  return cleanText(input, MAX_NAME);
}

function truncate(s: string, max: number): string {
  const points = Array.from(s);
  return points.length <= max ? s : points.slice(0, max).join("").trimEnd();
}
