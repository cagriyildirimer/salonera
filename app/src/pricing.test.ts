import { describe, expect, it } from "vitest";
import { billedGuests, breakdownText, packageTotal, tariffText } from "./pricing";

const menu = { basePrice: 5_000_000, perGuestPrice: 100_000, minGuests: 200 }; // 50.000 + kişi başı 1.000, en az 200

describe("kişi sayısına göre paket fiyatı", () => {
  it("sabit + kişi başı × davetli", () => {
    expect(packageTotal(menu, 500)).toBe(55_000_000);
    expect(packageTotal(menu, 1000)).toBe(105_000_000);
  });

  it("en az kişi sınırının altı sınır üzerinden hesaplanır", () => {
    expect(billedGuests(menu, 150)).toBe(200);
    expect(packageTotal(menu, 150)).toBe(25_000_000);
  });

  it("kişi başı fiyatı olmayan paket sabit kalır", () => {
    const fixed = { basePrice: 20_000_000, perGuestPrice: 0, minGuests: 0 };
    expect(packageTotal(fixed, 100)).toBe(20_000_000);
    expect(packageTotal(fixed, 900)).toBe(20_000_000);
    expect(breakdownText(fixed, 20_000_000)).toBe("");
  });

  it("tarife ve sözleşme açılımı", () => {
    expect(tariffText(menu)).toBe("50.000 ₺ + kişi başı 1.000 ₺ (en az 200 kişi)");
    expect(tariffText({ basePrice: 0, perGuestPrice: 150_000, minGuests: 0 })).toBe("kişi başı 1.500 ₺");
    // Kayıtlı tutardan kişi sayısı bulunur (davetli sonradan fiyat güncellenmeden değişse de).
    expect(breakdownText(menu, 40_000_000)).toBe("350 kişi × 1.000 ₺ + 50.000 ₺ sabit");
  });
});
