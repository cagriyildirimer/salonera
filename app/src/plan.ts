import { addDays, addMonths, parseDate } from "./format";
import type { InstallmentInput } from "./types";

/**
 * Ödeme planı önerisi: varsa kapora bugün, kalan tutar `count` parçaya bölünür.
 * Son ödeme düğünden en geç 7 gün önce olur. Aylık taksitler sığmıyorsa vadeler
 * bugün ile son ödeme günü arasına eşit dağıtılır. Kuruş artığı son taksite eklenir.
 */
export function buildPlan(total: number, deposit: number, count: number, todayIso: string, eventIso: string): InstallmentInput[] {
  const plan: InstallmentInput[] = [];
  if (total <= 0) return plan;
  const kapora = Math.min(Math.max(deposit, 0), total);
  if (kapora > 0) plan.push({ label: "Kapora", dueDate: todayIso, amount: kapora });
  const rest = total - kapora;
  if (rest <= 0) return plan;

  let lastDue = addDays(eventIso, -7);
  if (lastDue < todayIso) lastDue = todayIso;
  const n = Math.max(1, Math.floor(count));
  if (n === 1) {
    plan.push({ label: "Kalan ödeme", dueDate: lastDue, amount: rest });
    return plan;
  }

  const each = Math.floor(rest / n);
  const monthlyFits = addMonths(todayIso, n) <= lastDue;
  const spanDays = Math.round((parseDate(lastDue).getTime() - parseDate(todayIso).getTime()) / 86_400_000);
  for (let i = 1; i <= n; i++) {
    const due = monthlyFits ? addMonths(todayIso, i) : addDays(todayIso, Math.round((spanDays * i) / n));
    plan.push({ label: `${i}. taksit`, dueDate: due, amount: i === n ? rest - each * (n - 1) : each });
  }
  return plan;
}

/** Önerilen kapora: toplamın %20'si, 1.000 TL'ye yuvarlanmış (toplamdan büyük olamaz). */
export function suggestDeposit(total: number): number {
  if (total <= 0) return 0;
  const rounded = Math.round((total * 0.2) / 100_000) * 100_000;
  return Math.min(Math.max(rounded, 100_000), total);
}
