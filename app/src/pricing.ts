import { tl } from "./format";
import type { PackagePricing, Product } from "./types";

// Sunucudaki model.rs → PackagePricing ile aynı hesap. Tutarlar KURUŞ.
// Paket tutarı = sabit tutar + kişi başı × max(davetli, en az kişi).

export const pricingOf = (p: Product): PackagePricing =>
  ({ basePrice: p.price, perGuestPrice: p.perGuestPrice, minGuests: p.minGuests });

/** Ücretlendirilen kişi sayısı: davetli, "en az kişi" sınırının altındaysa sınır kadar. */
export function billedGuests(p: PackagePricing, guests: number): number {
  return p.perGuestPrice === 0 ? 0 : Math.max(guests, p.minGuests);
}

export function packageTotal(p: PackagePricing, guests: number): number {
  return p.basePrice + p.perGuestPrice * billedGuests(p, guests);
}

/** Paketin fiyat tarifesi, ör. "50.000 ₺ + kişi başı 1.000 ₺ (en az 200 kişi)". */
export function tariffText(p: PackagePricing): string {
  if (p.perGuestPrice === 0) return tl(p.basePrice, true);
  const perGuest = `kişi başı ${tl(p.perGuestPrice, true)}`;
  const min = p.minGuests > 0 ? ` (en az ${p.minGuests} kişi)` : "";
  return (p.basePrice > 0 ? `${tl(p.basePrice, true)} + ${perGuest}` : perGuest) + min;
}

/**
 * Sözleşmedeki paket tutarının açılımı, ör. "350 kişi × 1.000 ₺ + 50.000 ₺ sabit".
 * Kişi sayısı kayıtlı tutardan bulunur; davetli sayısı fiyat güncellenmeden değiştirildiyse de doğru kalır.
 * Kişi başı fiyatı olmayan pakette boş döner.
 */
export function breakdownText(p: PackagePricing, packagePrice: number): string {
  if (p.perGuestPrice === 0) return "";
  const guests = Math.round((packagePrice - p.basePrice) / p.perGuestPrice);
  const perGuest = `${guests} kişi × ${tl(p.perGuestPrice, true)}`;
  return p.basePrice > 0 ? `${perGuest} + ${tl(p.basePrice, true)} sabit` : perGuest;
}
