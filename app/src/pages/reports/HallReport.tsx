import { useState } from "react";
import { api } from "../../api";
import { BarChart, compactTl, type Series } from "../../components/Charts";
import { LoadError, Loading, useLoad } from "../../components/ui";
import { monthName, monthShort, tl } from "../../format";
import { confirmed, hallMonthGrid, weekendOccupancy } from "../../reportData";
import { downloadCsv, plain, ReportShell, Section, YearPicker } from "./common";

const SERIES_CLS: Series["cls"][] = ["s-wine", "s-gold", "s-plum", "s-green", "s-rose"];

/** Salon doluluğu: salon salon organizasyon sayısı, ciro, hafta sonu doluluk oranı ve ay ay dağılım. */
export function HallReport() {
  const [year, setYear] = useState(new Date().getFullYear());
  const sales = useLoad(() => api.salesReport(`${year}-01-01`, `${year}-12-31`), [year]);
  const hallList = useLoad(() => api.halls());
  const rows = sales.data ?? [];
  const conf = confirmed(rows);
  const names = [...new Set([...(hallList.data ?? []).map((h) => h.name), ...conf.map((r) => r.hallName)])];
  const grid = hallMonthGrid(rows, names);
  const maxCell = Math.max(1, ...Object.values(grid).flat());
  const stats = names.map((name) => {
    const list = conf.filter((r) => r.hallName === name);
    const hall = hallList.data?.find((h) => h.name === name);
    return {
      name,
      capacity: hall?.capacity ?? 0,
      count: list.length,
      sales: list.reduce((s, r) => s + r.total, 0),
      guests: list.length ? Math.round(list.reduce((s, r) => s + r.guestCount, 0) / list.length) : 0,
      weekend: weekendOccupancy(rows, name, year),
    };
  });
  const ready = !!sales.data && !!hallList.data;

  const excel = () => downloadCsv(`salonera-${year}-salon-dolulugu.csv`, [
    [`${year} salon doluluğu`],
    ["Salon", "Kapasite", "Kesin organizasyon", "Satış (TL)", "Ortalama davetli", "Dolu hafta sonu günü", "Hafta sonu günleri", "Hafta sonu doluluk %"],
    ...stats.map((s) => [s.name, s.capacity, s.count, plain(s.sales), s.guests, s.weekend.used, s.weekend.total, s.weekend.percent]),
    [],
    ["Salon", ...Array.from({ length: 12 }, (_, i) => monthName(i + 1)), "Toplam"],
    ...names.map((n) => [n, ...grid[n], grid[n].reduce((a, b) => a + b, 0)]),
  ]);

  return (
    <ReportShell title="Salon doluluğu" period={`${year} yılı`} ready={ready} onExcel={excel}
      controls={<YearPicker year={year} onChange={setYear} />}>
      {sales.error && <LoadError message={sales.error} onRetry={sales.reload} />}
      {!ready ? <Loading /> : (
        <>
          <div className="hall-cards">
            {stats.map((s, i) => (
              <div key={s.name} className="card hall-card rise">
                <div className="row" style={{ gap: 10 }}>
                  <span className={`hall-dot ${SERIES_CLS[i % SERIES_CLS.length]}`} />
                  <div className="card-title" style={{ fontSize: 24 }}>{s.name}</div>
                  <span className="spacer" />
                  {s.capacity > 0 && <span className="muted small">{s.capacity} kişilik</span>}
                </div>
                <div className="hall-figures">
                  <div><b>{s.count}</b><span>organizasyon</span></div>
                  <div><b>{compactTl(s.sales)}</b><span>satış</span></div>
                  <div><b>{s.guests}</b><span>ort. davetli</span></div>
                </div>
                <div className="occ">
                  <div className="row" style={{ justifyContent: "space-between" }}>
                    <span className="small strong">Hafta sonu doluluğu</span>
                    <span className="occ-pct">%{s.weekend.percent}</span>
                  </div>
                  <div className="money-bar" style={{ margin: "6px 0 4px" }}><div style={{ width: `${s.weekend.percent}%` }} /></div>
                  <span className="muted small">{s.weekend.total} Cumartesi-Pazar gününün {s.weekend.used} günü dolu</span>
                </div>
              </div>
            ))}
          </div>

          <Section title="Aylara göre organizasyon sayısı" sub="Kesin kayıtlar, salon salon">
            <BarChart labels={Array.from({ length: 12 }, (_, i) => monthShort(i + 1))} format={(n) => String(Math.round(n))} height={220} integer
              series={names.map((n, i) => ({ name: n, cls: SERIES_CLS[i % SERIES_CLS.length], values: grid[n] }))} />
          </Section>

          <Section title="Doluluk tablosu" sub="Koyu renk = o ay daha çok organizasyon">
            <div style={{ overflowX: "auto" }}>
              <table className="table heat">
                <thead><tr><th>Salon</th>{Array.from({ length: 12 }, (_, i) => <th key={i} className="num">{monthShort(i + 1)}</th>)}<th className="num">Toplam</th></tr></thead>
                <tbody>
                  {names.map((n) => (
                    <tr key={n}>
                      <td className="strong">{n}</td>
                      {grid[n].map((v, i) => (
                        <td key={i} className="num heat-cell" style={{ background: v ? `rgb(138 40 70 / ${0.08 + (v / maxCell) * 0.62})` : undefined, color: v / maxCell > 0.55 ? "#fff" : undefined }}>
                          {v || ""}
                        </td>
                      ))}
                      <td className="num strong">{grid[n].reduce((a, b) => a + b, 0)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td className="strong">Toplam</td>
                    {Array.from({ length: 12 }, (_, i) => <td key={i} className="num strong">{names.reduce((s, n) => s + grid[n][i], 0)}</td>)}
                    <td className="num strong">{conf.length}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
            <div className="muted small" style={{ marginTop: 10 }}>Toplam satış: {tl(conf.reduce((s, r) => s + r.total, 0), true)}</div>
          </Section>
        </>
      )}
    </ReportShell>
  );
}
