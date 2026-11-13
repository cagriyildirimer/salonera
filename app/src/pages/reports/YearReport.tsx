import { useState } from "react";
import { api } from "../../api";
import { BarChart, compactTl } from "../../components/Charts";
import { Empty, LoadError, Loading, StatCard, useLoad } from "../../components/ui";
import { monthName, monthShort, tl } from "../../format";
import { downloadCsv, plain, ReportShell, Section, YearPicker } from "./common";

/** Yıllık özet: ay ay satış (o ay yapılacak kesin organizasyonlar) ve tahsilat (o ay kasaya giren). */
export function YearReport() {
  const [year, setYear] = useState(new Date().getFullYear());
  const report = useLoad(() => api.yearReport(year), [year]);
  const halls = useLoad(() => api.hallUsage(`${year}-01-01`, `${year}-12-31`), [year]);
  const d = report.data;

  const excel = () => d && downloadCsv(`salonera-${year}-yillik-ozet.csv`, [
    [`${year} yıllık özet`],
    ["Ay", "Organizasyon", "Satış (TL)", "Alınan ödeme (TL)", "İade (TL)"],
    ...d.months.map((m) => [monthName(m.month), m.eventCount, plain(m.sales), plain(m.collected), plain(m.refunded)]),
    ["Toplam", d.months.reduce((s, m) => s + m.eventCount, 0), plain(d.totalSales), plain(d.totalCollected), plain(d.totalRefunded)],
    [],
    ["Salon", "Organizasyon", "Satış (TL)"],
    ...(halls.data ?? []).map((h) => [h.hallName, h.eventCount, plain(h.sales)]),
  ]);

  return (
    <ReportShell title="Yıllık özet" period={`${year} yılı`} ready={!!d} onExcel={excel}
      controls={<YearPicker year={year} onChange={setYear} />}>
      {report.error && <LoadError message={report.error} onRetry={report.reload} />}
      {report.loading && !d ? <Loading /> : d && (
        <>
          <div className="grid grid-4 report-kpis">
            <StatCard icon="calendar" tone="wine" label="Kesin organizasyon" value={String(d.months.reduce((s, m) => s + m.eventCount, 0))} />
            <StatCard icon="chart" tone="plum" label={`${year} toplam satış`} value={tl(d.totalSales, true)} sub="Bu yıl yapılacak organizasyonların tutarı" />
            <StatCard icon="wallet" tone="green" label="Alınan ödeme" value={tl(d.totalCollected, true)} valueColor="var(--success)" sub="Bu yıl kasaya giren" />
            <StatCard icon="undo" tone="gold" label="Yapılan iade" value={tl(d.totalRefunded, true)} />
          </div>

          <Section title="Aylara göre satış ve tahsilat" sub="Satış: o ay yapılacak organizasyonlar · Ödeme: o ay alınan para">
            <BarChart labels={d.months.map((m) => monthShort(m.month))} format={compactTl}
              highlight={(i) => year === new Date().getFullYear() && i === new Date().getMonth()}
              series={[
                { name: "Satış", cls: "s-wine", values: d.months.map((m) => m.sales) },
                { name: "Alınan ödeme", cls: "s-gold", values: d.months.map((m) => m.collected) },
              ]} />
          </Section>

          <div className="split">
            <Section title="Ay ay dökümü">
              <table className="table">
                <thead><tr><th>Ay</th><th className="num">Organizasyon</th><th className="num">Satış</th><th className="num">Alınan ödeme</th><th className="num">İade</th></tr></thead>
                <tbody>
                  {d.months.map((m) => (
                    <tr key={m.month}>
                      <td className="strong">{monthName(m.month)}</td>
                      <td className="num">{m.eventCount || "—"}</td>
                      <td className="num">{m.sales ? tl(m.sales) : "—"}</td>
                      <td className="num">{m.collected ? tl(m.collected) : "—"}</td>
                      <td className="num">{m.refunded ? tl(m.refunded) : "—"}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td className="strong">Toplam</td>
                    <td className="num strong">{d.months.reduce((s, m) => s + m.eventCount, 0)}</td>
                    <td className="num strong">{tl(d.totalSales)}</td>
                    <td className="num strong">{tl(d.totalCollected)}</td>
                    <td className="num strong">{tl(d.totalRefunded)}</td>
                  </tr>
                </tfoot>
              </table>
            </Section>
            <Section title="Salonlara göre" sub="Kesin organizasyon sayısı ve satış">
              {(halls.data ?? []).length === 0 ? <Empty icon="building">Veri yok.</Empty> : (
                <table className="table">
                  <thead><tr><th>Salon</th><th className="num">Organizasyon</th><th className="num">Satış</th></tr></thead>
                  <tbody>{halls.data!.map((h) => <tr key={h.hallName}><td className="strong">{h.hallName}</td><td className="num">{h.eventCount}</td><td className="num">{tl(h.sales, true)}</td></tr>)}</tbody>
                </table>
              )}
            </Section>
          </div>
        </>
      )}
    </ReportShell>
  );
}
