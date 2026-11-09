import { tl, dateLong, methodLabels } from "../format";
import { buildPlan } from "../plan";
import type { Method, Product, SaleInput } from "../types";
import { Icon } from "./Icon";
import { Empty, Field, MoneyInput } from "./ui";

export interface SaleState {
  packageId: string | null;
  extraIds: string[];
  discount: number;
  deposit: number;
  takeNow: boolean;
  method: Method;
  count: number;
}

export const emptySale = (): SaleState => ({
  packageId: null, extraIds: [], discount: 0, deposit: 0, takeNow: true, method: "nakit", count: 1,
});

export function saleTotals(sale: SaleState, packages: Product[], extras: Product[]) {
  const pkg = packages.find((p) => p.id === sale.packageId);
  const extrasTotal = extras.filter((e) => sale.extraIds.includes(e.id)).reduce((s, e) => s + e.price, 0);
  const gross = (pkg?.price ?? 0) + extrasTotal;
  return { pkg, gross, total: Math.max(gross - sale.discount, 0) };
}

export function toSaleInput(sale: SaleState, total: number, todayIso: string, eventIso: string): SaleInput {
  const deposit = Math.min(sale.deposit, total);
  return {
    packageId: sale.packageId!,
    extraIds: sale.extraIds,
    discount: sale.discount,
    plan: buildPlan(total, deposit, sale.count, todayIso, eventIso),
    firstPayment: sale.takeNow && deposit > 0 ? { amount: deposit, method: sale.method } : null,
  };
}

/** Paket, ek hizmetler, indirim ve canlı toplam. */
export function PackagePicker({ sale, onChange, packages, extras }: {
  sale: SaleState; onChange: (s: SaleState) => void; packages: Product[]; extras: Product[];
}) {
  const { gross, total } = saleTotals(sale, packages, extras);
  const toggleExtra = (id: string) =>
    onChange({ ...sale, extraIds: sale.extraIds.includes(id) ? sale.extraIds.filter((x) => x !== id) : [...sale.extraIds, id] });

  return (
    <div className="split">
      <div className="stack">
        <div className="card">
          <div className="card-title">Paket</div>
          <div className="card-sub" style={{ marginBottom: 16 }}>Müşterinin seçtiği paket</div>
          {packages.length === 0 ? (
            <Empty icon="package">Henüz paket tanımlanmamış. Yönetici, Ayarlar → Paketler bölümünden paket eklemelidir.</Empty>
          ) : (
            <div className="grid grid-2" style={{ gap: 12 }}>
              {packages.map((p) => (
                <button key={p.id} className={`choice ${sale.packageId === p.id ? "selected" : ""}`} style={{ alignItems: "flex-start" }}
                  onClick={() => onChange({ ...sale, packageId: p.id })}>
                  <div style={{ minWidth: 0 }}>
                    <div className="choice-title">{p.name}</div>
                    {p.description && <div className="choice-sub">{p.description}</div>}
                    <div className="choice-price">{tl(p.price, true)}</div>
                  </div>
                  <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
                </button>
              ))}
            </div>
          )}
        </div>
        {extras.length > 0 && (
          <div className="card">
            <div className="card-title">Ek hizmetler</div>
            <div className="card-sub" style={{ marginBottom: 16 }}>İstenenleri işaretleyin (isteğe bağlı)</div>
            <div className="grid grid-2" style={{ gap: 10 }}>
              {extras.map((e) => {
                const on = sale.extraIds.includes(e.id);
                return (
                  <button key={e.id} className={`choice ${on ? "selected" : ""}`} style={{ padding: "12px 14px" }} onClick={() => toggleExtra(e.id)}>
                    <span className="tick" style={{ marginLeft: 0, borderRadius: 7 }}><Icon name="check" size={15} strokeWidth={3} /></span>
                    <span style={{ flex: 1, fontWeight: 600 }}>{e.name}</span>
                    <span className="strong" style={{ color: "var(--primary)" }}>{tl(e.price, true)}</span>
                  </button>
                );
              })}
            </div>
          </div>
        )}
      </div>
      <div className="card" style={{ position: "sticky", top: 0 }}>
        <div className="card-title" style={{ marginBottom: 16 }}>Fiyat</div>
        <Field label="İndirim" hint="İndirim yoksa boş bırakın.">
          <MoneyInput value={sale.discount} onChange={(v) => onChange({ ...sale, discount: v })} />
        </Field>
        <div className="hr" />
        <div className="price-line"><span className="k">Paket + ek hizmetler</span><span>{tl(gross)}</span></div>
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
