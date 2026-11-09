import { describe, expect, it } from "vitest";
import { addMonths, parseMoney } from "./format";
import { buildPlan, suggestDeposit } from "./plan";

const sum = (plan: { amount: number }[]) => plan.reduce((s, x) => s + x.amount, 0);

describe("ödeme planı", () => {
  it("kapora + kalan, düğünden bir hafta önce", () => {
    const plan = buildPlan(21_000_000, 5_000_000, 1, "2026-03-02", "2026-09-12");
    expect(plan).toEqual([
      { label: "Kapora", dueDate: "2026-03-02", amount: 5_000_000 },
      { label: "Kalan ödeme", dueDate: "2026-09-05", amount: 16_000_000 },
    ]);
  });

  it("aylık taksitler sığıyorsa her ay; toplam kuruşu kuruşuna tutar", () => {
    const plan = buildPlan(10_000_001, 0, 3, "2026-03-02", "2026-12-12");
    expect(plan.map((p) => p.dueDate)).toEqual(["2026-04-02", "2026-05-02", "2026-06-02"]);
    expect(sum(plan)).toBe(10_000_001);
    expect(plan[2].amount).toBe(3_333_335);
  });

  it("düğün yakınsa vadeler bugün ile son gün arasına yayılır, aynı güne yığılmaz", () => {
    const plan = buildPlan(9_000_000, 3_000_000, 3, "2026-11-10", "2026-11-28");
    const dates = plan.slice(1).map((p) => p.dueDate);
    expect(new Set(dates).size).toBe(3);
    expect(dates[2]).toBe("2026-11-21");
    expect(sum(plan)).toBe(9_000_000);
  });

  it("düğün bir haftadan yakınsa kalan bugün", () => {
    const plan = buildPlan(1_000_000, 0, 1, "2026-11-10", "2026-11-12");
    expect(plan[0].dueDate).toBe("2026-11-10");
  });

  it("kapora toplamı aşamaz", () => {
    expect(buildPlan(500_000, 900_000, 2, "2026-01-01", "2026-06-01")).toEqual([
      { label: "Kapora", dueDate: "2026-01-01", amount: 500_000 },
    ]);
  });

  it("önerilen kapora %20, bin liraya yuvarlı", () => {
    expect(suggestDeposit(24_480_000)).toBe(4_900_000);
    expect(suggestDeposit(300_000)).toBe(100_000);
  });
});

describe("para ve tarih", () => {
  it("Türkçe ve noktalı yazımları anlar", () => {
    expect(parseMoney("50000")).toBe(5_000_000);
    expect(parseMoney("50.000")).toBe(5_000_000);
    expect(parseMoney("1.250.000")).toBe(125_000_000);
    expect(parseMoney("50.000,50")).toBe(5_000_050);
    expect(parseMoney("1500.5")).toBe(150_050);
    expect(parseMoney("12,345")).toBeNull();
    expect(parseMoney("abc")).toBeNull();
    expect(parseMoney("")).toBeNull();
  });

  it("ay sonunda ay ekleme taşmaz", () => {
    expect(addMonths("2026-01-31", 1)).toBe("2026-02-28");
  });
});

import { moneyWords, numberWords } from "./words";

describe("yazıyla tutar", () => {
  it("Türkçe sayı okunuşu", () => {
    expect(numberWords(1000)).toBe("Bin");
    expect(numberWords(1001)).toBe("BinBir");
    expect(numberWords(21_500)).toBe("YirmiBirBinBeşYüz");
    expect(numberWords(1_250_000)).toBe("BirMilyonİkiYüzElliBin");
    expect(moneyWords(1_550)).toBe("OnBeşTürkLirasıElliKuruş");
  });
});
