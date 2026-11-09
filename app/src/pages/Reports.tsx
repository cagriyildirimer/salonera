import { useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { Empty, LoadError, Loading, useLoad } from "../components/ui";
import { dateLong, monthName, tl } from "../format";

/** Excel'in Türkçe ayarlarında doğrudan açılan CSV (noktalı virgül, UTF-8 BOM). */
function downloadCsv(fileName: string, rows: (string | number)[][]) {
  const text = rows.map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(";")).join("\r\n");
  const blob = new Blob(["﻿" + text], { type: "text/csv;charset=utf-8" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(link.href);
}

const plain = (kurus: number) => (kurus / 100).toFixed(2).replace(".", ",");

export function Reports() {
  const { navigate } = useSession();
  const [year, setYear] = useState(new Date().getFullYear());
  const report = useLoad(() => api.yearReport(year), [year]);
  const receivables = useLoad(() => api.receivables());
  const halls = useLoad(() => api.hallUsage(`${year}-01-01`, `${year}-12-31`), [year]);
  const maxSales = Math.max(1, ...(report.data?.months.map((m) => Math.max(m.sales, m.collected)) ?? [1]));

  return (
    <div className="content">
      <div className="content-wide stack">
        <div className="row">
          <button className="btn icon-btn" onClick={() => setYear(year - 1)}><Icon name="left" /></button>
          <span className="strong" style={{ fontSize: 24, minWidth: 80, textAlign: "center" }}>{year}</span>
          <button className="btn icon-btn" onClick={() => setYear(year + 1)}><Icon name="right" /></button>
          <span className="spacer" />
          {report.data && (
            <button className="btn" onClick={() => downloadCsv(`salonera-${year}-aylik.csv`, [
              ["Ay", "Organizasyon", "Satış (TL)", "Alınan ödeme (TL)", "İade (TL)"],
              ...report.data!.months.map((m) => [monthName(m.month), m.eventCount, plain(m.sales), plain(m.collected), plain(m.refunded)]),
            ])}><Icon name="download" /> Excel'e aktar</button>
          )}
        </div>

        {report.error && <LoadError message={report.error} onRetry={report.reload} />}
        {report.loading && !report.data ? <Loading /> : report.data && (
          <>
            <div className="grid grid-3">
              <div className="card"><div className="muted">{year} toplam satış</div><div className="big-number">{tl(report.data.totalSales, true)}</div>
                <div className="muted small">Bu yıl yapılacak kesin organizasyonların tutarı</div></div>
              <div className="card"><div className="muted">Alınan ödeme</div><div className="big-number" style={{ color: "var(--success)" }}>{tl(report.data.totalCollected, true)}</div>
                <div className="muted small">Bu yıl kasaya giren para</div></div>
              <div className="card"><div className="muted">Yapılan iade</div><div className="big-number">{tl(report.data.totalRefunded, true)}</div></div>
            </div>

            <div className="card">
              <div className="card-head">
                <div><div className="card-title">Aylara göre</div><div className="card-sub">Satış: o ay yapılacak organizasyonlar · Ödeme: o ay alınan para</div></div>
                <div className="legend">
                  <span><i className="dot" style={{ background: "var(--primary)" }} /> Satış</span>
                  <span><i className="dot" style={{ background: "var(--gold)" }} /> Alınan ödeme</span>
                </div>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(12, 1fr)", gap: 10, alignItems: "end", height: 200, padding: "0 4px" }}>
                {report.data.months.map((m) => (
                  <div key={m.month} title={`${monthName(m.month)}: satış ${tl(m.sales)}, ödeme ${tl(m.collected)}`}
                    style={{ display: "flex", gap: 3, alignItems: "end", height: "100%" }}>
                    <div style={{ flex: 1, background: "var(--primary)", borderRadius: "6px 6px 0 0", height: `${(m.sales / maxSales) * 100}%`, minHeight: m.sales ? 3 : 0 }} />
                    <div style={{ flex: 1, background: "var(--gold)", borderRadius: "6px 6px 0 0", height: `${(m.collected / maxSales) * 100}%`, minHeight: m.collected ? 3 : 0 }} />
                  </div>
                ))}
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(12, 1fr)", gap: 10, marginTop: 6 }}>
                {report.data.months.map((m) => <div key={m.month} className="muted small" style={{ textAlign: "center" }}>{monthName(m.month).slice(0, 3)}</div>)}
              </div>
              <table className="table" style={{ marginTop: 18 }}>
                <thead><tr><th>Ay</th><th className="num">Organizasyon</th><th className="num">Satış</th><th className="num">Alınan ödeme</th><th className="num">İade</th></tr></thead>
                <tbody>
                  {report.data.months.map((m) => (
                    <tr key={m.month}>
                      <td>{monthName(m.month)}</td>
                      <td className="num">{m.eventCount || "—"}</td>
                      <td className="num">{m.sales ? tl(m.sales) : "—"}</td>
                      <td className="num">{m.collected ? tl(m.collected) : "—"}</td>
                      <td className="num">{m.refunded ? tl(m.refunded) : "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}

        <div className="split">
          <div className="card">
            <div className="card-head">
              <div><div className="card-title">Alacaklar</div><div className="card-sub">Ödemesi tamamlanmamış kesin kayıtlar</div></div>
              {receivables.data && receivables.data.length > 0 && (
                <button className="btn" onClick={() => downloadCsv("salonera-alacaklar.csv", [
                  ["Kayıt no", "Müşteri", "Telefon", "Tarih", "Toplam (TL)", "Ödenen (TL)", "Kalan (TL)", "Geciken (TL)"],
                  ...receivables.data!.map((r) => [r.no, r.customerName, r.customerPhone, dateLong(r.date), plain(r.total), plain(r.paid), plain(r.remaining), plain(r.overdue)]),
                ])}><Icon name="download" /> Excel'e aktar</button>
              )}
            </div>
            {receivables.loading && !receivables.data ? <Loading /> : (receivables.data ?? []).length === 0 ? <Empty icon="check">Bekleyen alacak yok.</Empty> : (
              <table className="table">
                <thead><tr><th>Müşteri</th><th>Tarih</th><th className="num">Kalan</th><th className="num">Geciken</th></tr></thead>
                <tbody>
                  {receivables.data!.map((r) => (
                    <tr key={r.id} className="clickable" onClick={() => navigate({ name: "reservation", id: r.id })}>
                      <td className="strong">{r.customerName}<div className="muted small">{r.customerPhone}</div></td>
                      <td>{dateLong(r.date)}</td>
                      <td className="num strong">{tl(r.remaining)}</td>
                      <td className="num" style={{ color: r.overdue ? "var(--danger)" : undefined }}>{r.overdue ? tl(r.overdue) : "—"}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr><td colSpan={2} className="strong">Toplam</td>
                    <td className="num strong">{tl(receivables.data!.reduce((s, r) => s + r.remaining, 0))}</td>
                    <td className="num strong" style={{ color: "var(--danger)" }}>{tl(receivables.data!.reduce((s, r) => s + r.overdue, 0))}</td></tr>
                </tfoot>
              </table>
            )}
          </div>
          <div className="card">
            <div className="card-title">Salonlara göre ({year})</div>
            <div className="card-sub" style={{ marginBottom: 14 }}>Kesin organizasyon sayısı ve satış</div>
            {(halls.data ?? []).length === 0 ? <Empty icon="building">Veri yok.</Empty> : (
              <table className="table">
                <thead><tr><th>Salon</th><th className="num">Organizasyon</th><th className="num">Satış</th></tr></thead>
                <tbody>{halls.data!.map((h) => <tr key={h.hallName}><td>{h.hallName}</td><td className="num">{h.eventCount}</td><td className="num">{tl(h.sales, true)}</td></tr>)}</tbody>
              </table>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
