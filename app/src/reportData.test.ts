import { describe, expect, it } from "vitest";
import { bucket, dailyTotals, dueByMonth, extrasBuckets, hallMonthGrid, weekendDays, weekendOccupancy } from "./reportData";
import type { DueRow, Payment, SalesRow } from "./types";

const sale = (over: Partial<SalesRow>): SalesRow => ({
  id: "x", no: "1", date: "2026-06-06", hallName: "Kristal", customerName: "A", kind: "dugun", guestCount: 100,
  status: "kesin", packageName: "Paket", packagePrice: 0, menuName: null, menuPrice: 0, discount: 0, total: 1000,
  paid: 0, extras: [], createdOn: "2026-01-01", createdBy: "Ayşe", cancelReason: null, ...over,
});
const pay = (over: Partial<Payment>): Payment => ({
  id: "p", reservationId: "r", reservationNo: "1", no: "M-1", date: "2026-06-01T10:00:00", amount: 100, method: "nakit",
  isRefund: false, note: null, isCancelled: false, cancelReason: null, createdBy: "Ayşe", customerName: "A", ...over,
});
const due = (over: Partial<DueRow>): DueRow => ({
  reservationId: "r", no: "1", customerName: "A", customerPhone: "0", eventDate: "2026-12-01", hallName: "K",
  label: "Kalan", dueDate: "2026-07-10", amount: 500, paid: 0, state: "bekliyor", ...over,
});

describe("rapor hesapları", () => {
  it("gruplar, sayar ve tutara göre sıralar", () => {
    const b = bucket([sale({ packageName: "A", total: 100 }), sale({ packageName: "B", total: 300 }), sale({ packageName: "A", total: 50 })],
      (r) => r.packageName, (r) => r.total);
    expect(b.map((x) => [x.key, x.count, x.amount])).toEqual([["B", 1, 300], ["A", 2, 150]]);
  });

  it("günlük tahsilatta iade düşülür, iptal edilen sayılmaz, boş günler sıfırdır", () => {
    const days = dailyTotals([
      pay({ amount: 1000 }), pay({ amount: 200, isRefund: true }), pay({ amount: 999, isCancelled: true }),
      pay({ date: "2026-06-03T09:00:00", amount: 50 }),
    ], "2026-06-01", "2026-06-03");
    expect(days.map((d) => d.amount)).toEqual([800, 0, 50]);
  });

  it("seçenekli ek hizmetler grup adıyla yazılır", () => {
    const b = extrasBuckets([
      sale({ extras: [{ name: "Çilekli", groupName: "Pasta", price: 4000 }, { name: "DJ", groupName: null, price: 8000 }] }),
      sale({ extras: [{ name: "Çilekli", groupName: "Pasta", price: 4000 }] }),
    ]);
    expect(b.map((x) => [x.label, x.count, x.amount])).toEqual([["Pasta: Çilekli", 2, 8000], ["DJ", 1, 8000]]);
  });

  it("vadeler aylara dağılır, geçmiş vadeler gecikmiş sütununa gider, kısmi ödeme düşülür", () => {
    const m = dueByMonth([due({ dueDate: "2026-05-01", amount: 300 }), due({ dueDate: "2026-06-20", amount: 500, paid: 200 }),
      due({ dueDate: "2027-09-01" })], "2026-06-15", 3);
    expect(m.map((x) => [x.key, x.amount])).toEqual([["gecikmis", 300], ["2026-06", 300], ["2026-07", 0], ["2026-08", 0]]);
  });

  it("hafta sonu doluluğu ve salon-ay tablosu yalnızca kesin kayıtları sayar", () => {
    expect(weekendDays(2026)).toHaveLength(104);
    const rows = [sale({ date: "2026-06-06" }), sale({ date: "2026-06-06" }), sale({ date: "2026-06-07", status: "iptal" }),
      sale({ date: "2026-06-10" })];
    expect(weekendOccupancy(rows, "Kristal", 2026)).toEqual({ used: 1, total: 104, percent: 1 });
    expect(hallMonthGrid(rows, ["Kristal", "Bahçe"]).Kristal[5]).toBe(3);
    expect(hallMonthGrid(rows, ["Kristal", "Bahçe"]).Bahçe[5]).toBe(0);
  });
});
