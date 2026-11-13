// Rapor hesapları: sunucudan gelen satırları gruplayan saf işlevler (ekrandan bağımsız, test edilebilir).
import { addDays, isoDate, parseDate } from "./format";
import type { DueRow, Payment, SalesRow } from "./types";

export interface Bucket { key: string; label: string; count: number; amount: number }

/** Anahtara göre sayar ve tutarları toplar; en büyük tutar (eşitse en çok adet) önce. */
export function bucket<T>(rows: T[], keyOf: (row: T) => string | null | undefined, amountOf: (row: T) => number = () => 0,
  labelOf: (key: string) => string = (k) => k): Bucket[] {
  const map = new Map<string, Bucket>();
  for (const row of rows) {
    const key = keyOf(row);
    if (!key) continue;
    const b = map.get(key) ?? { key, label: labelOf(key), count: 0, amount: 0 };
    b.count += 1;
    b.amount += amountOf(row);
    map.set(key, b);
  }
  return [...map.values()].sort((a, b) => b.amount - a.amount || b.count - a.count || a.label.localeCompare(b.label, "tr"));
}

/** Ödemenin kasaya etkisi: iade eksi, iptal edilen kayıt sıfır. */
export const netAmount = (p: Payment) => (p.isCancelled ? 0 : p.isRefund ? -p.amount : p.amount);

/** [from, to] arasındaki her gün için net tahsilat. */
export function dailyTotals(payments: Payment[], from: string, to: string): { date: string; amount: number }[] {
  const out: { date: string; amount: number }[] = [];
  const sums = new Map<string, number>();
  for (const p of payments) sums.set(p.date.slice(0, 10), (sums.get(p.date.slice(0, 10)) ?? 0) + netAmount(p));
  for (let d = from; d <= to; d = addDays(d, 1)) out.push({ date: d, amount: sums.get(d) ?? 0 });
  return out;
}

export const confirmed = (rows: SalesRow[]) => rows.filter((r) => r.status === "kesin");

/** Ek hizmetler: seçenek grubundakiler "Pasta: Çilekli pasta" gibi, gruptan bağımsız olanlar kendi adıyla. */
export function extrasBuckets(rows: SalesRow[]): Bucket[] {
  const flat = rows.flatMap((r) => r.extras);
  return bucket(flat, (e) => e.name, (e) => e.price, (name) => {
    const g = flat.find((e) => e.name === name)?.groupName;
    return g ? `${g}: ${name}` : name;
  });
}

/** Ay ay vadesi gelecek tutar. İlk sütun: bugünden önce vadesi geçmiş (gecikmiş) toplam. */
export function dueByMonth(rows: DueRow[], today: string, months = 12): { key: string; overdue: boolean; amount: number; count: number }[] {
  const out: { key: string; overdue: boolean; amount: number; count: number }[] = [{ key: "gecikmis", overdue: true, amount: 0, count: 0 }];
  const start = parseDate(today);
  for (let i = 0; i < months; i++) {
    out.push({ key: isoDate(new Date(start.getFullYear(), start.getMonth() + i, 1)).slice(0, 7), overdue: false, amount: 0, count: 0 });
  }
  for (const r of rows) {
    const open = r.amount - r.paid;
    const slot = r.dueDate < today ? out[0] : out.find((o) => o.key === r.dueDate.slice(0, 7));
    if (!slot) continue;
    slot.amount += open;
    slot.count += 1;
  }
  return out;
}

/** Bir yılın Cumartesi ve Pazar günleri (doluluk oranının paydası). */
export function weekendDays(year: number): string[] {
  const out: string[] = [];
  for (let d = `${year}-01-01`; d <= `${year}-12-31`; d = addDays(d, 1)) {
    const day = parseDate(d).getDay();
    if (day === 0 || day === 6) out.push(d);
  }
  return out;
}

/** Salonun hafta sonu doluluğu: kesin kaydı olan farklı hafta sonu günü / yılın hafta sonu günleri. */
export function weekendOccupancy(rows: SalesRow[], hallName: string, year: number): { used: number; total: number; percent: number } {
  const weekends = new Set(weekendDays(year));
  const used = new Set(confirmed(rows).filter((r) => r.hallName === hallName && weekends.has(r.date)).map((r) => r.date)).size;
  return { used, total: weekends.size, percent: weekends.size ? Math.round((used / weekends.size) * 100) : 0 };
}

/** Salon × ay: kesin organizasyon sayısı. */
export function hallMonthGrid(rows: SalesRow[], halls: string[]): Record<string, number[]> {
  const grid: Record<string, number[]> = Object.fromEntries(halls.map((h) => [h, Array(12).fill(0)]));
  for (const r of confirmed(rows)) {
    const row = grid[r.hallName] ?? (grid[r.hallName] = Array(12).fill(0));
    row[parseDate(r.date).getMonth()] += 1;
  }
  return grid;
}
