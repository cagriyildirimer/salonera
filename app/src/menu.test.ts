import { describe, expect, it } from "vitest";
import { addDish, cleanDishes, dishSummary, groupDishes, moveCourse, renameCourse } from "./menu";
import { menuTariffText, menuTotal } from "./pricing";

const menu = [
  { course: "Çorba", name: "Mercimek" },
  { course: "Ana yemek", name: "Kuzu tandır" },
  { course: "Tatlı", name: "Sütlaç" },
  { course: "Ana yemek", name: "Pilav" },
];

describe("yemek menüsü", () => {
  it("bölümler ilk göründükleri sırayla gruplanır", () => {
    const groups = groupDishes(menu);
    expect(groups.map((g) => g.course)).toEqual(["Çorba", "Ana yemek", "Tatlı"]);
    expect(groups[1].dishes.map((d) => d.name)).toEqual(["Kuzu tandır", "Pilav"]);
    expect(groups[1].dishes[1].index).toBe(3);
  });

  it("yemek bölümünün sonuna eklenir", () => {
    const next = addDish(menu, "Çorba", "Ezogelin");
    expect(next[1]).toEqual({ course: "Çorba", name: "Ezogelin" });
    expect(addDish(menu, "Meyve").at(-1)).toEqual({ course: "Meyve", name: "" });
  });

  it("temizlerken boş satırlar atılır, bölümler bir araya toplanır", () => {
    const cleaned = cleanDishes([...menu, { course: "Tatlı", name: "  " }, { course: " ", name: " Ayran " }]);
    expect(cleaned).toEqual([
      { course: "Çorba", name: "Mercimek" },
      { course: "Ana yemek", name: "Kuzu tandır" },
      { course: "Ana yemek", name: "Pilav" },
      { course: "Tatlı", name: "Sütlaç" },
      { course: "Diğer", name: "Ayran" },
    ]);
  });

  it("bölüm adı değişir ve bölüm taşınır", () => {
    expect(renameCourse(menu, "Ana yemek", "Ana yemekler").filter((x) => x.course === "Ana yemekler")).toHaveLength(2);
    const moved = moveCourse(menu, "Tatlı", -1);
    expect(groupDishes(moved).map((g) => g.course)).toEqual(["Çorba", "Tatlı", "Ana yemek"]);
    expect(moveCourse(menu, "Çorba", -1)).toBe(menu);
  });

  it("özet uzun menüyü kısaltır", () => {
    expect(dishSummary(menu, 2)).toBe("Mercimek, Kuzu tandır ve 2 yemek daha");
  });

  it("menü farkı en az kişi sınırıyla hesaplanır", () => {
    expect(menuTotal(15_000, 150, 200)).toBe(3_000_000);
    expect(menuTotal(15_000, 300, 0)).toBe(4_500_000);
    expect(menuTotal(0, 300, 0)).toBe(0);
    expect(menuTariffText(0)).toBe("Paket fiyatına dahil");
    expect(menuTariffText(15_000)).toBe("kişi başı +150 ₺");
  });
});
