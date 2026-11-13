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

describe("seçenekler (ör. pasta) ve ek hizmetler", () => {
  it("sabit ve kişi başı fiyat birlikte", async () => {
    const { extraTotal, extraTariffText, extraBreakdown, extraLabel } = await import("./pricing");
    const nuts = { price: 100_000, perGuestPrice: 2_000 };
    expect(extraTotal(nuts, 250, 0)).toBe(600_000);
    expect(extraTotal(nuts, 150, 200)).toBe(500_000); // paketin en az kişi sınırı
    expect(extraTotal({ price: 400_000, perGuestPrice: 0 }, 900, 0)).toBe(400_000);
    expect(extraTariffText(nuts)).toBe("1.000 ₺ + kişi başı 20 ₺");
    const line = { extraId: "x", name: "Kuruyemiş", groupName: null, price: 700_000, basePrice: 100_000, perGuest: 2_000 };
    expect(extraBreakdown(line)).toBe("300 kişi × 20 ₺ + 1.000 ₺ sabit");
    expect(extraLabel({ name: "Çilekli pasta", groupName: "Pasta" })).toBe("Pasta: Çilekli pasta");
  });

  it("aynı gruptan tek seçim", async () => {
    const { toggleOption } = await import("./components/SaleEditor");
    const make = (id: string, groupName: string | null) =>
      ({ id, name: id, description: null, price: 0, perGuestPrice: 0, minGuests: 0, groupName, isActive: true });
    const all = [make("muz", "Pasta"), make("cilek", "Pasta"), make("foto", null)];
    expect(toggleOption(["muz", "foto"], all[1], all)).toEqual(["foto", "cilek"]);
    expect(toggleOption(["cilek"], all[1], all)).toEqual([]);
    expect(toggleOption(["muz"], all[2], all)).toEqual(["muz", "foto"]);
  });
});
