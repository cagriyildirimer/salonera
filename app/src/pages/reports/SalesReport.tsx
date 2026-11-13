import { useState } from "react";
import { useSession } from "../../App";
import { api } from "../../api";
import { BarChart, compactTl, HBars } from "../../components/Charts";
import { LoadError, Loading, StatCard, useLoad } from "../../components/ui";
import { dateLong, kindLabels, monthShort, parseDate, statusLabel, tl } from "../../format";
import { bucket, confirmed, extrasBuckets } from "../../reportData";
import { downloadCsv, plain, ReportShell, Section, YearPicker } from "./common";

const avg = (sum: number, n: number) => (n ? Math.round(sum / n) : 0);

/** Satış analizi: ne satılıyor (paket, menü, ek hizmet, tür), kim satıyor, indirimler ve iptaller. */
export function SalesReport() {
  const { navigate } = useSession();
  const [year, setYear] = useState(new Date().getFullYear());
  const sales = useLoad(() => api.salesReport(`${year}-01-01`, `${year}-12-31`), [year]);
  const rows = sales.data ?? [];
  const conf = confirmed(rows);
  const cancelled = rows.filter((r) => r.status === "iptal");
  const options = rows.filter((r) => r.status === "on_rezervasyon");
  const total = conf.reduce((s, r) => s + r.total, 0);
  const perMonth = (list: typeof rows) => Array.from({ length: 12 }, (_, i) => list.filter((r) => parseDate(r.date).getMonth() === i).length);

  const packages = bucket(conf, (r) => r.packageName, (r) => r.packagePrice);
  const menus = bucket(conf, (r) => r.menuName, (r) => r.menuPrice);
  const extras = extrasBuckets(conf);
  const kinds = bucket(conf, (r) => r.kind, (r) => r.total, (k) => kindLabels[k as keyof typeof kindLabels]);
  const sellers = bucket(conf, (r) => r.createdBy, (r) => r.total);
  const discounted = conf.filter((r) => r.discount > 0);

  const excel = () => downloadCsv(`salonera-${year}-satis-analizi.csv`, [
    [`${year} satış analizi`],
    ["Kayıt no", "Tarih", "Müşteri", "Salon", "Tür", "Davetli", "Durum", "Paket", "Paket (TL)", "Menü", "Menü farkı (TL)",
      "Ek hizmetler", "Ek hizmet (TL)", "İndirim (TL)", "Toplam (TL)", "Ödenen (TL)", "Satışı yapan", "Kayıt günü", "İptal nedeni"],
    ...rows.map((r) => [r.no, dateLong(r.date), r.customerName, r.hallName, kindLabels[r.kind], r.guestCount, statusLabel(r.status),
      r.packageName ?? "", plain(r.packagePrice), r.menuName ?? "", plain(r.menuPrice),
      r.extras.map((e) => (e.groupName ? `${e.groupName}: ${e.name}` : e.name)).join(", "), plain(r.extras.reduce((s, e) => s + e.price, 0)),
      plain(r.discount), plain(r.total), plain(r.paid), r.createdBy, dateLong(r.createdOn), r.cancelReason ?? ""]),
    [],
    ["Paket", "Adet", "Tutar (TL)"], ...packages.map((b) => [b.label, b.count, plain(b.amount)]),
    [],
    ["Menü", "Adet", "Menü farkı (TL)"], ...menus.map((b) => [b.label, b.count, plain(b.amount)]),
    [],
    ["Ek hizmet", "Adet", "Tutar (TL)"], ...extras.map((b) => [b.label, b.count, plain(b.amount)]),
  ]);

  const countAndMoney = (r: { count: number; amount: number }) => <><b>{r.count}</b> adet · {compactTl(r.amount)}</>;

  return (
    <ReportShell title="Satış analizi" period={`${year} yılı organizasyonları`} ready={!!sales.data} onExcel={excel}
      controls={<YearPicker year={year} onChange={setYear} />}>
      {sales.error && <LoadError message={sales.error} onRetry={sales.reload} />}
      {sales.loading && !sales.data ? <Loading /> : (
        <>
          <div className="grid grid-4 report-kpis">
            <StatCard icon="calendar" tone="wine" label="Kesin organizasyon" value={String(conf.length)}
              sub={`${options.length} ön rezervasyon · ${cancelled.length} iptal`} />
            <StatCard icon="chart" tone="gold" label="Toplam satış" value={tl(total, true)} />
            <StatCard icon="file" tone="plum" label="Ortalama sözleşme" value={tl(avg(total, conf.length), true)} />
            <StatCard icon="users" tone="green" label="Ortalama davetli" value={String(avg(conf.reduce((s, r) => s + r.guestCount, 0), conf.length))}
              sub={`${discounted.length} kayıtta ${tl(discounted.reduce((s, r) => s + r.discount, 0), true)} indirim`} />
          </div>

          <Section title="Aylara göre kayıtlar" sub="Organizasyon ayına göre kesin, ön rezervasyon ve iptal sayıları">
            <BarChart labels={Array.from({ length: 12 }, (_, i) => monthShort(i + 1))} format={(n) => String(Math.round(n))} height={200} integer
              series={[
                { name: "Kesin", cls: "s-wine", values: perMonth(conf) },
                { name: "Ön rezervasyon", cls: "s-gold", values: perMonth(options) },
                { name: "İptal", cls: "s-danger", values: perMonth(cancelled) },
              ]} />
          </Section>

          <div className="grid grid-2">
            <Section title="Paketler" sub="Kaç kez satıldı · paket tutarı">
              <HBars rows={packages} value="count" right={countAndMoney} />
            </Section>
            <Section title="Yemek menüleri" sub="Kaç düğünde seçildi · menü farkı">
              <HBars rows={menus} value="count" cls="s-plum" right={countAndMoney} empty="Menü seçilmiş kayıt yok." />
            </Section>
            <Section title="Ek hizmetler ve seçenekler" sub="Pasta, müzik gibi seçenekler grup adıyla">
              <HBars rows={extras} value="count" cls="s-gold" right={countAndMoney} limit={12} empty="Ek hizmet satılmamış." />
            </Section>
            <Section title="Organizasyon türleri">
              <HBars rows={kinds} value="count" cls="s-rose" right={countAndMoney} />
            </Section>
            <Section title="Satışı yapan" sub="Kaydı açan kullanıcıya göre">
              <HBars rows={sellers} value="amount" cls="s-green" right={countAndMoney} />
            </Section>
            <Section title="İptaller" sub={`${cancelled.length} kayıt iptal edildi`}>
              {cancelled.length === 0 ? <div className="muted">Bu yıl iptal yok.</div> : (
                <table className="table">
                  <thead><tr><th>Tarih</th><th>Müşteri</th><th>Neden</th><th className="num">Kalan para</th></tr></thead>
                  <tbody>
                    {cancelled.map((r) => (
                      <tr key={r.id} className="clickable" onClick={() => navigate({ name: "reservation", id: r.id })}>
                        <td>{dateLong(r.date)}</td>
                        <td className="strong">{r.customerName}</td>
                        <td className="muted">{r.cancelReason ?? "—"}</td>
                        <td className="num">{r.paid ? tl(r.paid, true) : "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </Section>
          </div>
        </>
      )}
    </ReportShell>
  );
}
