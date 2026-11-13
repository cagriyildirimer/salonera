// Basit grafikler (dışarıdan kütüphane yok): sütun grafik, yatay çubuk listesi ve halka grafik.
// Hepsi düz HTML/CSS'tir; ekranda ve yazıcı çıktısında aynı görünür.
import type { ReactNode } from "react";

/** Eksenler için kısa para: 1.250.000 ₺ → "1,25 Mn ₺", 45.000 ₺ → "45 B ₺". */
export function compactTl(kurus: number): string {
  const v = kurus / 100;
  const abs = Math.abs(v);
  const fmt = (n: number, d: number) => n.toLocaleString("tr-TR", { maximumFractionDigits: d });
  if (abs >= 1_000_000) return `${fmt(v / 1_000_000, abs >= 10_000_000 ? 1 : 2)} Mn ₺`;
  if (abs >= 1_000) return `${fmt(v / 1_000, 0)} B ₺`;
  return `${fmt(v, 0)} ₺`;
}

export interface Series { name: string; cls: "s-wine" | "s-gold" | "s-plum" | "s-green" | "s-rose" | "s-danger"; values: number[] }

/** Gruplu sütun grafik. Üzerine gelince değer görünür; yazıcıda eksen değerleri okunur. */
export function BarChart({ labels, series, format, height = 240, highlight, integer }: {
  labels: string[]; series: Series[]; format: (n: number) => string; height?: number; highlight?: (index: number) => boolean;
  /** Adet grafikleri: eksen değerleri tam sayı olsun (4'e bölünebilen üst sınır). */
  integer?: boolean;
}) {
  const max = Math.max(1, ...series.flatMap((s) => s.values));
  const nice = integer ? Math.max(4, Math.ceil(max / 4) * 4) : niceMax(max);
  const ticks = [1, 0.75, 0.5, 0.25, 0].map((f) => nice * f);
  return (
    <div className="chart">
      {series.length > 1 && (
        <div className="legend chart-legend">
          {series.map((s) => <span key={s.name}><i className={`dot ${s.cls}`} /> {s.name}</span>)}
        </div>
      )}
      <div className="chart-body" style={{ height }}>
        <div className="chart-axis">{ticks.map((t) => <span key={t}>{format(t)}</span>)}</div>
        <div className="chart-plot">
          {ticks.map((t) => <div key={t} className="chart-grid" style={{ bottom: `${(t / nice) * 100}%` }} />)}
          <div className="chart-cols" style={{ gridTemplateColumns: `repeat(${labels.length}, minmax(0, 1fr))`, gap: labels.length > 20 ? 3 : 10 }}>
            {labels.map((label, i) => (
              <div key={label + i} className={`chart-col ${highlight?.(i) ? "hl" : ""}`}
                title={`${label}\n${series.map((s) => `${s.name}: ${format(s.values[i] ?? 0)}`).join("\n")}`}>
                {series.map((s) => (
                  <div key={s.name} className={`chart-bar ${s.cls}`}
                    style={{ height: `${(Math.max(0, s.values[i] ?? 0) / nice) * 100}%`, minHeight: (s.values[i] ?? 0) > 0 ? 3 : 0 }} />
                ))}
              </div>
            ))}
          </div>
        </div>
      </div>
      <div className="chart-labels" style={{ gridTemplateColumns: `repeat(${labels.length}, minmax(0, 1fr))`, gap: labels.length > 20 ? 3 : 10 }}>
        {labels.map((l, i) => <span key={l + i}>{labels.length > 20 && i % 2 === 1 ? "" : l}</span>)}
      </div>
    </div>
  );
}

/** Eksenin üst sınırı: 1, 2, 2,5 veya 5'in katlarına yuvarlanır (ör. 4.780 → 5.000). */
export function niceMax(n: number): number {
  if (n <= 0) return 1;
  const p = 10 ** Math.floor(Math.log10(n));
  const f = n / p;
  return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * p;
}

/** Yatay çubuk listesi: en çok satan paket, menü, ek hizmet… */
export function HBars({ rows, value, cls = "s-wine", right, empty = "Veri yok.", limit }: {
  rows: { key: string; label: string; count: number; amount: number }[];
  value: "count" | "amount";
  cls?: Series["cls"];
  right: (row: { count: number; amount: number }) => ReactNode;
  empty?: string;
  limit?: number;
}) {
  if (rows.length === 0) return <div className="muted">{empty}</div>;
  const sorted = [...rows].sort((a, b) => b[value] - a[value] || b.amount - a.amount);
  const shown = limit ? sorted.slice(0, limit) : sorted;
  const max = Math.max(1, ...shown.map((r) => r[value]));
  return (
    <div className="hbars">
      {shown.map((r) => (
        <div key={r.key} className="hbar-row">
          <div className="hbar-label" title={r.label}>{r.label}</div>
          <div className="hbar-track"><div className={`hbar-fill ${cls}`} style={{ width: `${(r[value] / max) * 100}%` }} /></div>
          <div className="hbar-value">{right(r)}</div>
        </div>
      ))}
      {limit && rows.length > limit && <div className="muted small">+{rows.length - limit} kalem daha (Excel çıktısında hepsi var)</div>}
    </div>
  );
}

const DONUT_COLORS = ["#8a2846", "#d9ac58", "#5a1d4b", "#3f9a69", "#c0405f", "#9a7f88"];

/** Halka grafik: parçaların toplamdaki payı. Ortada toplam yazar. */
export function Donut({ parts, center, sub, format }: {
  parts: { label: string; value: number }[]; center: string; sub?: string; format: (n: number) => string;
}) {
  const total = parts.reduce((s, p) => s + Math.max(0, p.value), 0);
  let acc = 0;
  const stops = parts.map((p, i) => {
    const from = total ? (acc / total) * 360 : 0;
    acc += Math.max(0, p.value);
    const to = total ? (acc / total) * 360 : 0;
    return `${DONUT_COLORS[i % DONUT_COLORS.length]} ${from}deg ${to}deg`;
  });
  return (
    <div className="donut-wrap">
      <div className="donut" style={{ background: total ? `conic-gradient(${stops.join(", ")})` : "#f3e9e1" }}>
        <div className="donut-hole"><b>{center}</b>{sub && <span>{sub}</span>}</div>
      </div>
      <div className="donut-legend">
        {parts.map((p, i) => (
          <div key={p.label} className="row" style={{ gap: 10, flexWrap: "nowrap" }}>
            <i className="dot" style={{ background: DONUT_COLORS[i % DONUT_COLORS.length], width: 12, height: 12 }} />
            <span style={{ flex: 1 }}>{p.label}</span>
            <b>{format(p.value)}</b>
            <span className="muted small" style={{ width: 44, textAlign: "right" }}>%{total ? Math.round((Math.max(0, p.value) / total) * 100) : 0}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
