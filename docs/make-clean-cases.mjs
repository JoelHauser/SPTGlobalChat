// Writes docs/clean-cases.json with every non-ASCII character escaped, so the file
// stays readable and carries no invisible characters. Code points are given as numbers
// here because this script itself must stay ASCII too.
//
//   node docs/make-clean-cases.mjs docs/clean-cases.json
//
// Edit the cases here, never the JSON by hand. Both cleaners are tested against it.
import { writeFileSync } from "node:fs";

const c = (...cps) => String.fromCodePoint(...cps);

const cases = [
  ["plain text passes", "anyone got a LEDX?", "anyone got a LEDX?"],
  ["rich-text tags lose < and >", "<size=5000>hi</size>", "size=5000hi/size"],
  ["RTL override removed", "photo" + c(0x202e) + "gnp.exe", "photognp.exe"],
  ["zero-width removed", "Sc" + c(0x200b) + "oop", "Scoop"],
  ["BOM removed", c(0xfeff) + "hello", "hello"],
  ["newlines become spaces", "line one\nline two", "line one line two"],
  ["whitespace runs collapse", "a    b\t\tc", "a b c"],
  // "x" has no precomposed accented forms, so NFC cannot fold a mark into it first.
  ["zalgo capped at two marks", "x" + c(0x301, 0x302, 0x303, 0x304), "x" + c(0x301, 0x302)],
  ["NFC folds before counting", "e" + c(0x301, 0x302, 0x303, 0x304), c(0xe9, 0x302, 0x303)],
  ["leading mark dropped", c(0x301) + "abc", "abc"],
  ["accents survive (NFC)", "cafe" + c(0x301) + " na" + c(0xef) + "ve", "caf" + c(0xe9) + " na" + c(0xef) + "ve"],
  ["emoji not split", "gg " + c(0x1f600), "gg " + c(0x1f600)],
  ["lone high surrogate removed", "a" + String.fromCharCode(0xd800) + "b", "ab"],
  ["lone low surrogate removed", "a" + String.fromCharCode(0xdc00) + "b", "ab"],
  ["paragraph separator is a space", "a" + c(0x2029) + "b", "a b"],
  ["empty after cleaning", c(0x200b, 0x202e) + "<>", ""],
  ["not a string", 42, ""],
].map(([name, input, out]) => ({ name, in: input, out }));

const escaped = JSON.stringify(cases, null, 2).replace(
  /[\u007f-\uffff]/g,
  (ch) => "\\u" + ch.charCodeAt(0).toString(16).padStart(4, "0"),
);

writeFileSync(process.argv[2], escaped + "\n");
