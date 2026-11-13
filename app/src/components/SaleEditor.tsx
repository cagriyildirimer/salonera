import { tl, dateLong, methodLabels } from "../format";
import { buildPlan } from "../plan";
import { dishSummary } from "../menu";
import {
  billedGuests, extraTariffText, extraTotal, menuTariffText, menuTotal, packageTotal, pricingOf, tariffText,
} from "../pricing";
import type { Menu, Method, Product, SaleInput } from "../types";
import { Icon } from "./Icon";
import { Empty, Field, MoneyInput } from "./ui";

export interface SaleState {
  packageId: string | null;
  /** Yemek menüsü; boşsa menü sonra seçilir. */
  menuId: string | null;
  extraIds: string[];
  discount: number;
  deposit: number;
  takeNow: boolean;
  method: Method;
  count: number;
}

export const emptySale = (): SaleState => ({
  packageId: null, menuId: null, extraIds: [], discount: 0, deposit: 0, takeNow: true, method: "nakit", count: 1,
});

/** Paketin "en az kişi" sınırı (kişi başı fiyatsız pakette sınır yoktur). Menü ve kişi başı seçenekler de bununla çarpılır. */
const minGuestsOf = (pkg: Product | undefined) => (pkg && pkg.perGuestPrice > 0 ? pkg.minGuests : 0);

/** Seçilen paket (kişi sayısıyla), menü farkı, ek hizmetler ve indirimle toplam. Sunucudaki hesapla aynıdır. */
export function saleTotals(sale: SaleState, packages: Product[], extras: Product[], guests: number, menus: Menu[] = []) {
  const pkg = packages.find((p) => p.id === sale.packageId);
  const packagePrice = pkg ? packageTotal(pricingOf(pkg), guests) : 0;
  const minGuests = minGuestsOf(pkg);
  const menu = menus.find((m) => m.id === sale.menuId);
  const menuPrice = menu ? menuTotal(menu.perGuestPrice, guests, minGuests) : 0;
  const chosen = extras.filter((e) => sale.extraIds.includes(e.id)).map((e) => ({ extra: e, price: extraTotal(e, guests, minGuests) }));
  const extrasTotal = chosen.reduce((s, x) => s + x.price, 0);
  const gross = packagePrice + menuPrice + extrasTotal;
  return { pkg, packagePrice, menu, menuPrice, chosen, gross, total: Math.max(gross - sale.discount, 0) };
}

export function toSaleInput(sale: SaleState, total: number, todayIso: string, eventIso: string): SaleInput {
  const deposit = Math.min(sale.deposit, total);
  return {
    packageId: sale.packageId!,
    menuId: sale.menuId,
    extraIds: sale.extraIds,
    discount: sale.discount,
    plan: buildPlan(total, deposit, sale.count, todayIso, eventIso),
    firstPayment: sale.takeNow && deposit > 0 ? { amount: deposit, method: sale.method } : null,
  };
}

/** Seçimi değiştirir: gruplu seçenekte aynı gruptaki diğerleri bırakılır (ör. tek pasta). */
export function toggleOption(ids: string[], extra: Product, extras: Product[]): string[] {
  if (ids.includes(extra.id)) return ids.filter((x) => x !== extra.id);
  const sameGroup = extra.groupName ? extras.filter((e) => e.groupName === extra.groupName).map((e) => e.id) : [];
  return [...ids.filter((x) => !sameGroup.includes(x)), extra.id];
}

/** Altta sağda hep görünen toplam. `was` verilirse eski tutar üstü çizili yanında gösterilir. */
export function TotalBar({ total, was, label = "Toplam" }: { total: number; was?: number; label?: string }) {
  return (
    <div className="foot-total" aria-live="polite">
      <span className="k">{label}</span>
      <span className="v">{was !== undefined && was !== total && <span className="was">{tl(was, true)}</span>}{tl(total)}</span>
    </div>
  );
}

/** Seçenek kartının altındaki "seçilirse toplam" satırı. */
function WhatIf({ on, total, current }: { on: boolean; total: number; current: number }) {
  if (on) return <div className="choice-total">Seçili · toplam<div><b>{tl(total, true)}</b></div></div>;
  const diff = total - current;
  return (
    <div className="choice-total">
      Seçilirse toplam
      <div>
        <b>{tl(total, true)}</b>
        {diff !== 0 && <span className="muted" style={{ whiteSpace: "nowrap" }}> ({diff > 0 ? "+" : "−"}{tl(Math.abs(diff), true)})</span>}
      </div>
    </div>
  );
}

/**
 * Ek hizmetler ve seçenek grupları (ör. Pasta: muzlu / çilekli). Gruptan tek seçim yapılır.
 * Her kartta o seçenek seçilirse toplamın ne olacağı yazar; müşterinin "şu olursa ne kadar?" sorusu orada cevaplanır.
 */
export function OptionsPicker({ extras, selected, onChange, priceOf, totalWith, current, columns = 2 }: {
  extras: Product[];
  selected: string[];
  onChange: (ids: string[]) => void;
  /** Seçeneğin bu rezervasyondaki tutarı. */
  priceOf: (extra: Product) => number;
  /** Bu seçimle toplam ne olur? */
  totalWith: (ids: string[]) => number;
  current: number;
  /** Yan tarafta fiyat paneli varsa 2, tam genişlikte 3 sütun. */
  columns?: 2 | 3;
}) {
  const grid = `grid grid-${columns}`;
  const groups = [...new Set(extras.map((e) => e.groupName).filter((g): g is string => !!g))];
  const singles = extras.filter((e) => !e.groupName);
  const priceText = (e: Product) => (
    <>
      <div className="strong" style={{ color: "var(--primary)", fontSize: 17, marginTop: 4 }}>+ {tl(priceOf(e), true)}</div>
      {e.perGuestPrice > 0 && <div className="choice-sub">{extraTariffText(e)}</div>}
    </>
  );
  return (
    <>
      {groups.map((group) => {
        const options = extras.filter((e) => e.groupName === group);
        const ids = options.map((e) => e.id);
        const none = !selected.some((x) => ids.includes(x));
        const without = selected.filter((x) => !ids.includes(x));
        return (
          <div key={group} className="card">
            <div className="card-title">{group}</div>
            <div className="card-sub" style={{ marginBottom: 16 }}>Birini seçin. Her seçeneğin altında, o seçilirse toplam tutar yazar.</div>
            <div className={grid} style={{ gap: 12 }}>
              <button className={`choice ${none ? "selected" : ""}`} style={{ alignItems: "flex-start" }} onClick={() => onChange(without)}>
                <div style={{ minWidth: 0, flex: 1 }}>
                  <div className="choice-title">İstemiyor</div>
                  <div className="choice-sub">{group} yok</div>
                  <WhatIf on={none} total={totalWith(without)} current={current} />
                </div>
                <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
              </button>
              {options.map((e) => {
                const on = selected.includes(e.id);
                return (
                  <button key={e.id} className={`choice ${on ? "selected" : ""}`} style={{ alignItems: "flex-start" }}
                    onClick={() => !on && onChange(toggleOption(selected, e, extras))}>
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <div className="choice-title">{e.name}{!e.isActive && <span className="badge badge-muted" style={{ marginLeft: 6 }}>Satıştan kalktı</span>}</div>
                      {priceText(e)}
                      <WhatIf on={on} total={on ? current : totalWith([...without, e.id])} current={current} />
                    </div>
                    <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
                  </button>
                );
              })}
            </div>
          </div>
        );
      })}
      {singles.length > 0 && (
        <div className="card">
          <div className="card-title">Ek hizmetler</div>
          <div className="card-sub" style={{ marginBottom: 16 }}>İstenenleri işaretleyin (isteğe bağlı)</div>
          <div className={grid} style={{ gap: 12 }}>
            {singles.map((e) => {
              const on = selected.includes(e.id);
              const next = toggleOption(selected, e, extras);
              return (
                <button key={e.id} className={`choice ${on ? "selected" : ""}`} style={{ alignItems: "flex-start" }} onClick={() => onChange(next)}>
                  <span className="tick" style={{ marginLeft: 0, borderRadius: 7 }}><Icon name="check" size={15} strokeWidth={3} /></span>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div className="choice-title">{e.name}{!e.isActive && <span className="badge badge-muted" style={{ marginLeft: 6 }}>Satıştan kalktı</span>}</div>
                    {priceText(e)}
                    {on
                      ? <div className="choice-total">Çıkarılırsa toplam<div><b>{tl(totalWith(next), true)}</b></div></div>
                      : <WhatIf on={false} total={totalWith(next)} current={current} />}
                  </div>
                </button>
              );
            })}
          </div>
        </div>
      )}
    </>
  );
}

/** Paket, yemek menüsü, seçenekler, ek hizmetler, indirim ve canlı toplam. */
export function PackagePicker({ sale, onChange, packages, extras, menus = [], guests }: {
  sale: SaleState; onChange: (s: SaleState) => void; packages: Product[]; extras: Product[]; menus?: Menu[]; guests: number;
}) {
  const { pkg, packagePrice, menu, menuPrice, chosen, gross, total } = saleTotals(sale, packages, extras, guests, menus);
  const minGuests = minGuestsOf(pkg);
  const totalOf = (patch: Partial<SaleState>) => saleTotals({ ...sale, ...patch }, packages, extras, guests, menus).total;

  return (
    <div className="split">
      <div className="stack">
        <div className="card">
          <div className="card-title">Paket</div>
          <div className="card-sub" style={{ marginBottom: 16 }}>Müşterinin seçtiği paket · fiyatlar {guests} davetliye göre</div>
          {packages.length === 0 ? (
            <Empty icon="package">Henüz paket tanımlanmamış. Yönetici, Ayarlar → Paketler bölümünden paket eklemelidir.</Empty>
          ) : (
            <div className="grid grid-2" style={{ gap: 12 }}>
              {packages.map((p) => {
                const pricing = pricingOf(p);
                const billed = billedGuests(pricing, guests);
                const on = sale.packageId === p.id;
                return (
                <button key={p.id} className={`choice ${on ? "selected" : ""}`} style={{ alignItems: "flex-start" }}
                  onClick={() => onChange({ ...sale, packageId: p.id })}>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div className="choice-title">{p.name}</div>
                    {p.description && <div className="choice-sub">{p.description}</div>}
                    <div className="choice-price">{tl(packageTotal(pricing, guests), true)}</div>
                    {p.perGuestPrice > 0 && (
                      <div className="choice-sub">
                        {tariffText(pricing)}
                        {billed > guests && <><br />{guests} davetli, {billed} kişi üzerinden hesaplandı</>}
                      </div>
                    )}
                    {pkg && <WhatIf on={on} total={on ? total : totalOf({ packageId: p.id })} current={total} />}
                  </div>
                  <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
                </button>
                );
              })}
            </div>
          )}
        </div>
        {menus.length > 0 && (
          <div className="card">
            <div className="card-title">Yemek menüsü</div>
            <div className="card-sub" style={{ marginBottom: 16 }}>
              Müşteri henüz karar vermediyse boş bırakın; menü sonra rezervasyon kartından seçilebilir.
            </div>
            <div className="grid grid-2" style={{ gap: 12 }}>
              <button className={`choice ${sale.menuId === null ? "selected" : ""}`} style={{ alignItems: "flex-start" }}
                onClick={() => onChange({ ...sale, menuId: null })}>
                <div style={{ minWidth: 0, flex: 1 }}>
                  <div className="choice-title">Sonra seçilecek</div>
                  <div className="choice-sub">Tadım sonrası veya müşteri karar verince</div>
                  {pkg && <WhatIf on={sale.menuId === null} total={totalOf({ menuId: null })} current={total} />}
                </div>
                <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
              </button>
              {menus.map((m) => (
                <button key={m.id} className={`choice ${sale.menuId === m.id ? "selected" : ""}`} style={{ alignItems: "flex-start" }}
                  onClick={() => onChange({ ...sale, menuId: m.id })}>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <div className="choice-title">{m.name}</div>
                    <div className="choice-sub">{m.description || dishSummary(m.items, 3)}</div>
                    <div className="choice-price" style={{ fontSize: m.perGuestPrice ? undefined : 15 }}>
                      {m.perGuestPrice ? `+ ${tl(menuTotal(m.perGuestPrice, guests, minGuests), true)}` : "Paket fiyatına dahil"}
                    </div>
                    {m.perGuestPrice > 0 && <div className="choice-sub">{menuTariffText(m.perGuestPrice)}</div>}
                    {pkg && <WhatIf on={sale.menuId === m.id} total={totalOf({ menuId: m.id })} current={total} />}
                  </div>
                  <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
                </button>
              ))}
            </div>
          </div>
        )}
        <OptionsPicker extras={extras} selected={sale.extraIds} onChange={(extraIds) => onChange({ ...sale, extraIds })}
          priceOf={(e) => extraTotal(e, guests, minGuests)} totalWith={(extraIds) => totalOf({ extraIds })} current={total} />
      </div>
      <div className="card" style={{ position: "sticky", top: 0 }}>
        <div className="card-title" style={{ marginBottom: 16 }}>Fiyat</div>
        <Field label="İndirim" hint="İndirim yoksa boş bırakın.">
          <MoneyInput value={sale.discount} onChange={(v) => onChange({ ...sale, discount: v })} />
        </Field>
        <div className="hr" />
        {!pkg && <div className="muted small" style={{ marginBottom: 8 }}>Paket seçilince fiyat burada hesaplanır.</div>}
        {pkg && (
          <div className="price-line">
            <span className="k">{pkg.name}{pkg.perGuestPrice > 0 ? ` (${billedGuests(pricingOf(pkg), guests)} kişi)` : ""}</span><span>{tl(packagePrice)}</span>
          </div>
        )}
        {menu && menuPrice > 0 && (
          <div className="price-line"><span className="k">Menü farkı ({menu.name})</span><span>{tl(menuPrice)}</span></div>
        )}
        {chosen.map(({ extra, price }) => (
          <div key={extra.id} className="price-line">
            <span className="k">{extra.groupName ? `${extra.groupName}: ${extra.name}` : extra.name}</span><span>{tl(price)}</span>
          </div>
        ))}
        <div className="price-line"><span className="k">İndirim</span><span style={{ color: "var(--success)" }}>{sale.discount ? `− ${tl(sale.discount)}` : "—"}</span></div>
        {sale.discount > gross && <div className="form-error">İndirim toplam fiyattan büyük olamaz.</div>}
        <div className="total-box" style={{ marginTop: 12 }}>
          <span className="strong" style={{ fontSize: 17 }}>Toplam</span>
          <span className="v">{tl(total)}</span>
        </div>
      </div>
    </div>
  );
}

/** Kapora, nasıl ödendiği ve kalan tutarın taksitlendirilmesi. */
export function PaymentPlanner({ sale, onChange, total, todayIso, eventIso }: {
  sale: SaleState; onChange: (s: SaleState) => void; total: number; todayIso: string; eventIso: string;
}) {
  const deposit = Math.min(sale.deposit, total);
  const plan = buildPlan(total, deposit, sale.count, todayIso, eventIso);
  const methods: Method[] = ["nakit", "kredi_karti", "havale"];
  return (
    <div className="grid grid-2" style={{ alignItems: "start" }}>
      <div className="card stack">
        <div>
          <div className="card-title">Kapora ve ödeme planı</div>
          <div className="card-sub">Toplam tutar: <b style={{ color: "var(--primary)" }}>{tl(total)}</b></div>
        </div>
        <Field label="Kapora tutarı" hint="Kapora alınmayacaksa boş bırakın.">
          <MoneyInput value={sale.deposit} onChange={(v) => onChange({ ...sale, deposit: v })} />
        </Field>
        {sale.deposit > total && <div className="form-error">Kapora, toplam tutardan büyük olamaz.</div>}
        {deposit > 0 && (
          <>
            <label className="checkbox">
              <input type="checkbox" checked={sale.takeNow} onChange={(e) => onChange({ ...sale, takeNow: e.target.checked })} />
              <span><b>Kaporayı şimdi aldım</b><br /><span className="muted small">Makbuz hazırlanır ve ödeme olarak kaydedilir.</span></span>
            </label>
            {sale.takeNow && (
              <div className="field">
                <label>Nasıl ödendi?</label>
                <div className="segment">
                  {methods.map((m) => (
                    <button key={m} className={sale.method === m ? "on" : ""} onClick={() => onChange({ ...sale, method: m })}>
                      {methodLabels[m]}
                    </button>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
        <Field label="Kalan tutar nasıl ödenecek?">
          <select className="select" value={sale.count} onChange={(e) => onChange({ ...sale, count: Number(e.target.value) })}>
            <option value={1}>Tek seferde (düğünden 1 hafta önce)</option>
            {Array.from({ length: 11 }, (_, i) => i + 2).map((n) => <option key={n} value={n}>{n} taksit</option>)}
          </select>
        </Field>
      </div>
      <div className="card">
        <div className="card-title">Ödeme takvimi</div>
        <div className="card-sub" style={{ marginBottom: 8 }}>Müşterinin ne zaman ne kadar ödeyeceği</div>
        <div className="list">
          {plan.map((p, i) => (
            <div key={i} className="list-item" style={{ cursor: "default" }}>
              <div style={{ flex: 1 }}>
                <div className="strong">{p.label}</div>
                <div className="item-sub">{dateLong(p.dueDate)}</div>
              </div>
              <div className="strong" style={{ fontSize: 17 }}>{tl(p.amount)}</div>
            </div>
          ))}
        </div>
        <p className="muted small" style={{ marginTop: 12 }}>Plan daha sonra rezervasyon kartından değiştirilebilir.</p>
      </div>
    </div>
  );
}
