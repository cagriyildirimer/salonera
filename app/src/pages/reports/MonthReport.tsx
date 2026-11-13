import { useState } from "react";
import { useSession } from "../../App";
import { api } from "../../api";
import { BarChart, compactTl, HBars } from "../../components/Charts";
import { LoadError, Loading, StatCard, StatusBadge, useLoad } from "../../components/ui";
import { dateLong, kindLabels, monthName, parseDate, statusLabel, tl, today } from "../../format";
import { bucket, confirmed, dailyTotals, netAmount } from "../../reportData";
import { downloadCsv, MonthPicker, monthRange, plain, ReportShell, Section } from "./common";

/** Aylık rapor: seçilen ayın organizasyonları, satışı ve kasası. */
export function MonthReport() {
  const { navigate } = useSession();
  const [month, setMonth] = useState(today().slice(0, 7));
  const { from, to } = monthRange(month);
  const sales = useLoad(() => api.salesReport(from, to), [from, to]);
  const payments = useLoad(() => api.payments(from, to), [from, to]);
  const [y, m] = month.split("-").map(Number);
  const title = `${monthName(m)} ${y}`;

  const rows = sales.data ?? [];
  const conf = confirmed(rows);
  const pays = payments.data ?? [];
  const net = pays.reduce((s, p) => s + netAmount(p), 0);
  const days = dailyTotals(pays, from, to);
  const ready = !!sales.data && !!payments.data;

  const excel = () => downloadCsv(`salonera-${month}-aylik-rapor.csv`, [
    [`${title} aylık rapor`],
    ["Kesin organizasyon", conf.length, "Davetli", conf.reduce((s, r) => s + r.guestCount, 0),
      "Satış (TL)", plain(conf.reduce((s, r) => s + r.total, 0)), "Net tahsilat (TL)", plain(net)],
    [],
    ["Tarih", "Kayıt no", "Müşteri", "Salon", "Tür", "Davetli", "Paket", "Menü", "Tutar (TL)", "Ödenen (TL)", "Kalan (TL)", "Durum"],
    ...rows.map((r) => [dateLong(r.date), r.no, r.customerName, r.hallName, kindLabels[r.kind], r.guestCount, r.packageName ?? "",
      r.menuName ?? "", plain(r.total), plain(r.paid), plain(r.status === "kesin" ? Math.max(0, r.total - r.paid) : 0), statusLabel(r.status)]),
    [],
    ["Gün", "Net tahsilat (TL)"],
    ...days.map((d) => [dateLong(d.date), plain(d.amount)]),
  ]);

  return (
    <ReportShell title="Aylık rapor" period={title} ready={ready} onExcel={excel}
      controls={<MonthPicker month={month} onChange={setMonth} />}>
      {sales.error && <LoadError message={sales.error} onRetry={sales.reload} />}
      {!ready ? <Loading /> : (
        <>
          <div className="grid grid-4 report-kpis">
            <StatCard icon="calendar" tone="wine" label="Kesin organizasyon" value={String(conf.length)}
              sub={`${rows.filter((r) => r.status === "on_rezervasyon").length} ön rezervasyon · ${rows.filter((r) => r.status === "iptal").length} iptal`} />
            <StatCard icon="users" tone="plum" label="Toplam davetli" value={conf.reduce((s, r) => s + r.guestCount, 0).toLocaleString("tr-TR")}
              sub={conf.length ? `Ortalama ${Math.round(conf.reduce((s, r) => s + r.guestCount, 0) / conf.length)} kişi` : undefined} />
            <StatCard icon="chart" tone="gold" label="Satış" value={tl(conf.reduce((s, r) => s + r.total, 0), true)} sub="Bu ayki organizasyonların tutarı" />
            <StatCard icon="wallet" tone="green" label="Net tahsilat" value={tl(net, true)} valueColor="var(--success)" sub={`${pays.filter((p) => !p.isCancelled).length} makbuz`} />
          </div>

          <Section title="Günlük tahsilat" sub="Her gün kasaya giren net para (iadeler düşülmüş)">
            <BarChart labels={days.map((d) => String(parseDate(d.date).getDate()))} format={compactTl} height={200}
              highlight={(i) => [0, 6].includes(parseDate(days[i].date).getDay())}
              series={[{ name: "Tahsilat", cls: "s-gold", values: days.map((d) => d.amount) }]} />
          </Section>

          <div className="grid grid-2">
            <Section title="Organizasyon türleri" sub="Kesin kayıtlar">
              <HBars rows={bucket(conf, (r) => r.kind, (r) => r.total, (k) => kindLabels[k as keyof typeof kindLabels])} value="count"
                right={(r) => <><b>{r.count}</b> · {compactTl(r.amount)}</>} />
            </Section>
            <Section title="Salonlar" sub="Kesin kayıtlar">
              <HBars rows={bucket(conf, (r) => r.hallName, (r) => r.total)} value="amount" cls="s-plum"
                right={(r) => <><b>{r.count}</b> · {compactTl(r.amount)}</>} />
            </Section>
          </div>

          <Section title="Ayın organizasyonları" sub={`${rows.length} kayıt`}>
            <table className="table">
              <thead><tr><th>Tarih</th><th>Müşteri</th><th>Salon</th><th>Tür</th><th className="num">Davetli</th><th>Paket</th><th className="num">Tutar</th><th className="num">Kalan</th><th>Durum</th></tr></thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id} className={`clickable ${r.status === "iptal" ? "cancelled" : ""}`} onClick={() => navigate({ name: "reservation", id: r.id })}>
                    <td>{dateLong(r.date)}</td>
                    <td className="strong">{r.customerName}</td>
                    <td>{r.hallName}</td>
                    <td>{kindLabels[r.kind]}</td>
                    <td className="num">{r.guestCount}</td>
                    <td className="muted">{r.packageName ?? "—"}</td>
                    <td className="num">{r.total ? tl(r.total, true) : "—"}</td>
                    <td className="num">{r.status === "kesin" && r.total > r.paid ? tl(r.total - r.paid, true) : "—"}</td>
                    <td><StatusBadge r={{ status: r.status, optionExpired: false }} /></td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={4} className="strong">Kesin kayıtlar toplamı</td>
                  <td className="num strong">{conf.reduce((s, r) => s + r.guestCount, 0)}</td>
                  <td />
                  <td className="num strong">{tl(conf.reduce((s, r) => s + r.total, 0), true)}</td>
                  <td className="num strong">{tl(conf.reduce((s, r) => s + Math.max(0, r.total - r.paid), 0), true)}</td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </Section>
        </>
      )}
    </ReportShell>
  );
}
