import type { EventKind, InstallmentState, Method, Role, Status } from "./types";

const money = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const moneyShort = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 0 });

/** Kuruş → "12.500,00 ₺". Küsurat yoksa `short` ile "12.500 ₺". */
export function tl(kurus: number, short = false): string {
  const value = kurus / 100;
  if (short && kurus % 100 === 0) return `${moneyShort.format(value)} ₺`;
  return `${money.format(value)} ₺`;
}

/**
 * Kullanıcının yazdığı tutarı kuruşa çevirir. "50000", "50.000", "50.000,50", "1500.5" hepsi kabul edilir.
 * Geçersizse null.
 */
export function parseMoney(text: string): number | null {
  let value = text.replace(/[₺\s]|TL/gi, "").trim();
  if (value === "") return null;
  if (!/^[\d.,]+$/.test(value)) return null;
  const lastComma = value.lastIndexOf(",");
  const lastDot = value.lastIndexOf(".");
  if (lastComma >= 0) {
    // Türkçe yazım: nokta binlik, virgül ondalık.
    value = value.replace(/\./g, "").replace(",", ".");
  } else if (lastDot >= 0) {
    // Sadece nokta var: son noktadan sonra tam 3 rakam varsa binlik ayırıcıdır.
    const decimals = value.length - lastDot - 1;
    value = decimals === 3 && value.split(".").every((p, i) => i === 0 || p.length === 3) ? value.replace(/\./g, "") : value;
  }
  if ((value.match(/\./g) ?? []).length > 1) return null;
  const number = Number(value);
  if (!Number.isFinite(number) || number < 0) return null;
  const kurus = Math.round(number * 100);
  if (Math.abs(kurus - number * 100) > 1e-6) return null; // 2 haneden fazla kuruş
  return kurus;
}

/** Kuruşu düzenleme kutusunda gösterilecek metne çevirir: 5000000 → "50.000". */
export function moneyInput(kurus: number): string {
  if (kurus === 0) return "";
  return kurus % 100 === 0 ? moneyShort.format(kurus / 100) : money.format(kurus / 100);
}

const months = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
const days = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];

export const monthName = (month: number) => months[month - 1];
export const monthShort = (month: number) => months[month - 1].slice(0, 3);

/** "2026-06-13" → Date (yerel saat, saat dilimi kaymadan). */
export function parseDate(iso: string): Date {
  const [y, m, d] = iso.slice(0, 10).split("-").map(Number);
  return new Date(y, m - 1, d);
}

export function isoDate(date: Date): string {
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${m}-${d}`;
}

export function addDays(iso: string, count: number): string {
  const date = parseDate(iso);
  date.setDate(date.getDate() + count);
  return isoDate(date);
}

export function addMonths(iso: string, count: number): string {
  const date = parseDate(iso);
  const day = date.getDate();
  date.setDate(1);
  date.setMonth(date.getMonth() + count);
  const last = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
  date.setDate(Math.min(day, last));
  return isoDate(date);
}

export const today = () => isoDate(new Date());

/** "13 Haziran 2026" */
export function dateLong(iso: string): string {
  const d = parseDate(iso);
  return `${d.getDate()} ${months[d.getMonth()]} ${d.getFullYear()}`;
}

/** "13 Haziran 2026, Cumartesi" */
export function dateWithDay(iso: string): string {
  return `${dateLong(iso)}, ${days[parseDate(iso).getDay()]}`;
}

export const weekday = (iso: string) => days[parseDate(iso).getDay()];

/** "2026-03-02T14:05:00" → "2 Mart 2026, 14:05" */
export function dateTime(value: string): string {
  return `${dateLong(value.slice(0, 10))}, ${value.slice(11, 16)}`;
}

/** Bugünden kaç gün sonra: "Bugün", "Yarın", "3 gün sonra", "2 gün önce" */
export function relativeDay(iso: string): string {
  const diff = Math.round((parseDate(iso).getTime() - parseDate(today()).getTime()) / 86_400_000);
  if (diff === 0) return "Bugün";
  if (diff === 1) return "Yarın";
  if (diff === -1) return "Dün";
  return diff > 0 ? `${diff} gün sonra` : `${-diff} gün önce`;
}

export const kindLabels: Record<EventKind, string> = {
  dugun: "Düğün", nisan: "Nişan", kina: "Kına", soz: "Söz", sunnet: "Sünnet", kokteyl: "Kokteyl", diger: "Diğer",
};
export const methodLabels: Record<Method, string> = { nakit: "Nakit", kredi_karti: "Kredi kartı", havale: "Havale / EFT" };
export const roleLabels: Record<Role, string> = { yonetici: "Yönetici", calisan: "Çalışan" };
export const installmentLabels: Record<InstallmentState, string> = {
  bekliyor: "Bekliyor", kismen: "Kısmen ödendi", odendi: "Ödendi", gecikti: "Gecikti",
};

export function statusLabel(status: Status, optionExpired = false): string {
  if (status === "on_rezervasyon") return optionExpired ? "Süresi doldu" : "Ön rezervasyon";
  return status === "kesin" ? "Kesin kayıt" : "İptal edildi";
}

/** Telefonu arama bağlantısına çevirir. */
export const telHref = (phone: string) => `tel:${phone.replace(/[^\d+]/g, "")}`;

/** "SS:DD" saatine dakika ekler (gece yarısını geçerse başa döner). */
export function addMinutes(time: string, minutes: number): string {
  const [h, m] = time.split(":").map(Number);
  const total = (((h * 60 + m + minutes) % 1440) + 1440) % 1440;
  return `${String(Math.floor(total / 60)).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
}

/** Kullanıcının yazdığı saati "SS:DD" yapar: "2130", "21.30", "21:30", "9", "930" → geçersizse null. */
export function normalizeClock(text: string): string | null {
  const t = text.trim().replace(/[.,\s]/g, ":");
  let h: number, m: number;
  const parts = t.match(/^(\d{1,2}):(\d{2})$/);
  if (parts) { h = Number(parts[1]); m = Number(parts[2]); }
  else if (/^\d{1,2}$/.test(t)) { h = Number(t); m = 0; }
  else if (/^\d{3,4}$/.test(t)) { h = Number(t.slice(0, -2)); m = Number(t.slice(-2)); }
  else return null;
  if (h > 23 || m > 59) return null;
  return `${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`;
}
