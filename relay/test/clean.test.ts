import { describe, expect, it } from "vitest";
import cases from "../../docs/clean-cases.json";
import { cleanText, MAX_TEXT } from "../src/clean";

// The same cases drive the plugin's TextSafety tests, so the two cleaners cannot drift.
describe("cleanText, shared cases", () => {
  for (const c of cases as { name: string; in: unknown; out: string }[]) {
    it(c.name, () => expect(cleanText(c.in)).toBe(c.out));
  }
});

describe("cleanText, length", () => {
  it("caps at MAX_TEXT code points", () => {
    expect(Array.from(cleanText("a".repeat(1000))).length).toBe(MAX_TEXT);
  });

  it("never splits a surrogate pair at the cap", () => {
    const out = cleanText("\u{1F600}".repeat(400));
    expect(Array.from(out).length).toBe(MAX_TEXT);
    expect(out).toBe("\u{1F600}".repeat(MAX_TEXT));
  });
});
