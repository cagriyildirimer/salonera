// Raporların ortak parçaları: üst araç çubuğu (dönem seçimi, Excel, Yazdır), yazıcı başlığı, CSV indirme.
import type { ReactNode } from "react";
import { useSession } from "../../App";
import { Icon } from "../../components/Icon";
import { dateTime, isoDate, monthName } from "../../format";

/** Excel'in Türkçe ayarlarında doğrudan açılan CSV (noktalı virgül, UTF-8 BOM). */
export function downloadCsv(fileName: string, rows: (string | number)[][]) {
  const text = rows.map((r) => r.map((c) => `"${String(c).replace(/"/g, '""')}"`).join(";")).join("\r\n");
  const blob = new Blob(["﻿" + text], { type: "text/csv;charset=utf-8" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(link.href);
}

/** Excel için düz sayı: 1234,50 (para birimi ve binlik ayırıcı olmadan, toplanabilir). */
export const plain = (kurus: number) => (kurus / 100).toFixed(2).replace(".", ",");

export function ReportShell({ title, period, controls, onExcel, ready, children }: {
  title: string; period: string; controls?: ReactNode; onExcel: () => void; ready: boolean; children: ReactNode;
}) {
  const { info } = useSession();
  return (
    <div className="stack report">
      <div className="card report-toolbar no-print">
        <div className="row" style={{ gap: 12 }}>
          {controls}
          <span className="spacer" />
          <button className="btn" disabled={!ready} onClick={onExcel}><Icon name="download" /> Excel'e aktar</button>
          <button className="btn btn-gold" disabled={!ready} onClick={() => window.print()}><Icon name="printer" /> Yazdır</button>
        </div>
      </div>
      <div className="print-only report-print-head">
        <div>
          <div className="rp-business">{info.businessName}</div>
          <div className="rp-title">{title}</div>
        </div>
        <div className="rp-meta">
          <div>{period}</div>
          <div>Yazdırma: {dateTime(isoDate(new Date()) + "T" + new Date().toTimeString().slice(0, 5))}</div>
        </div>
      </div>
      {children}
    </div>
  );
}

export function YearPicker({ year, onChange }: { year: number; onChange: (y: number) => void }) {
  return (
    <div className="period-picker">
      <button className="btn icon-btn" title="Önceki yıl" onClick={() => onChange(year - 1)}><Icon name="left" /></button>
      <span className="period-label">{year}</span>
      <button className="btn icon-btn" title="Sonraki yıl" onClick={() => onChange(year + 1)}><Icon name="right" /></button>
    </div>
  );
}

/** Ay seçici. `month` "2026-11" biçimindedir. */
export function MonthPicker({ month, onChange }: { month: string; onChange: (m: string) => void }) {
  const [y, m] = month.split("-").map(Number);
  const shift = (d: number) => {
    const date = new Date(y, m - 1 + d, 1);
    onChange(`${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`);
  };
  return (
    <div className="period-picker">
      <button className="btn icon-btn" title="Önceki ay" onClick={() => shift(-1)}><Icon name="left" /></button>
      <span className="period-label">{monthName(m)} <span className="yr">{y}</span></span>
      <button className="btn icon-btn" title="Sonraki ay" onClick={() => shift(1)}><Icon name="right" /></button>
    </div>
  );
}

export function monthRange(month: string): { from: string; to: string } {
  const [y, m] = month.split("-").map(Number);
  return { from: `${month}-01`, to: isoDate(new Date(y, m, 0)) };
}

export function Section({ title, sub, children, className = "", right }: {
  title: string; sub?: string; children: ReactNode; className?: string; right?: ReactNode;
}) {
  return (
    <div className={`card report-section ${className}`}>
      <div className="card-head">
        <div><div className="card-title">{title}</div>{sub && <div className="card-sub">{sub}</div>}</div>
        {right}
      </div>
      {children}
    </div>
  );
}
