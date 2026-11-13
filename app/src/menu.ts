import type { MenuDish } from "./types";

// Yemek menüsü yardımcıları. Menü düz bir yemek listesidir; her yemeğin bir bölümü vardır
// (Çorba, Ana yemek…). Ekranda bölümler ilk göründükleri sırayla gruplanır.

/** Bölüm eklerken önerilenler. */
export const COURSES = [
  "Çorba", "Soğuk başlangıç", "Ara sıcak", "Salata", "Ana yemek", "Garnitür", "Tatlı", "Meyve", "İçecek",
];

export interface CourseGroup { course: string; dishes: { name: string; index: number }[] }

/** Yemekleri bölümlere ayırır; bölümler ilk göründükleri sırada, yemekler yazıldıkları sırada kalır. */
export function groupDishes(items: MenuDish[]): CourseGroup[] {
  const groups: CourseGroup[] = [];
  items.forEach((item, index) => {
    let group = groups.find((g) => g.course === item.course);
    if (!group) {
      group = { course: item.course, dishes: [] };
      groups.push(group);
    }
    group.dishes.push({ name: item.name, index });
  });
  return groups;
}

/** Düz listeyi bölüm sırasına göre dizer (aynı bölümün yemekleri yan yana gelir). */
export function sortByCourse(items: MenuDish[]): MenuDish[] {
  return groupDishes(items).flatMap((g) => g.dishes.map((d) => ({ course: g.course, name: d.name })));
}

/** Boş yemek satırlarını atar, boşlukları kırpar; bölümü boş kalan yemek "Diğer" bölümüne düşer. */
export function cleanDishes(items: MenuDish[]): MenuDish[] {
  return sortByCourse(
    items
      .map((x) => ({ course: x.course.trim() || "Diğer", name: x.name.trim() }))
      .filter((x) => x.name),
  );
}

/** Bölümün sonuna yemek ekler (bölüm yoksa listenin sonuna). */
export function addDish(items: MenuDish[], course: string, name = ""): MenuDish[] {
  let last = -1;
  items.forEach((x, i) => { if (x.course === course) last = i; });
  const at = last === -1 ? items.length : last + 1;
  return [...items.slice(0, at), { course, name }, ...items.slice(at)];
}

export function renameCourse(items: MenuDish[], from: string, to: string): MenuDish[] {
  return items.map((x) => (x.course === from ? { ...x, course: to } : x));
}

/** Bölümü bir üste / alta taşır. */
export function moveCourse(items: MenuDish[], course: string, direction: -1 | 1): MenuDish[] {
  const groups = groupDishes(items);
  const at = groups.findIndex((g) => g.course === course);
  const to = at + direction;
  if (at === -1 || to < 0 || to >= groups.length) return items;
  [groups[at], groups[to]] = [groups[to], groups[at]];
  return groups.flatMap((g) => g.dishes.map((d) => ({ course: g.course, name: d.name })));
}

/** Kısa özet, ör. "Mercimek çorbası, Kuzu tandır, Fırın sütlaç". */
export function dishSummary(items: MenuDish[], max = 4): string {
  const names = items.map((x) => x.name);
  return names.length > max ? `${names.slice(0, max).join(", ")} ve ${names.length - max} yemek daha` : names.join(", ");
}
