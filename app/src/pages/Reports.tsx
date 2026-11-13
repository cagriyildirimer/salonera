import { useState } from "react";
import { Icon, type IconName } from "../components/Icon";
import { CashReport } from "./reports/CashReport";
import { DueReport } from "./reports/DueReport";
import { HallReport } from "./reports/HallReport";
import { MonthReport } from "./reports/MonthReport";
import { SalesReport } from "./reports/SalesReport";
import { YearReport } from "./reports/YearReport";

type ReportId = "year" | "month" | "cash" | "due" | "sales" | "halls";

const REPORTS: { id: ReportId; icon: IconName; title: string; desc: string; tone: string }[] = [
  { id: "year", icon: "chart", title: "Yıllık özet", desc: "Ay ay satış ve tahsilat", tone: "wine" },
  { id: "month", icon: "calendar", title: "Aylık rapor", desc: "Ayın organizasyonları ve kasası", tone: "plum" },
  { id: "cash", icon: "wallet", title: "Kasa raporu", desc: "Gün sonu ve dönem tahsilatı", tone: "green" },
  { id: "due", icon: "clock", title: "Alacaklar", desc: "Gecikenler ve gelecek taksitler", tone: "danger" },
  { id: "sales", icon: "star", title: "Satış analizi", desc: "Paket, menü, ek hizmet, iptal", tone: "gold" },
  { id: "halls", icon: "building", title: "Salon doluluğu", desc: "Salon salon doluluk ve ciro", tone: "wine" },
];

const KEY = "salonera-report";

/** Raporlar: üstte rapor seçimi; her rapor ekranda grafikli gösterilir, yazdırılır ve Excel'e aktarılır. */
export function Reports() {
  const [id, setId] = useState<ReportId>(() => {
    try {
      const saved = localStorage.getItem(KEY) as ReportId | null;
      return saved && REPORTS.some((r) => r.id === saved) ? saved : "year";
    } catch {
      return "year";
    }
  });
  const choose = (next: ReportId) => {
    setId(next);
    try { localStorage.setItem(KEY, next); } catch { /* tarayıcı belleği kapalıysa sorun değil */ }
  };

  return (
    <div className="content">
      <div className="content-wide stack">
        <div className="report-picker no-print">
          {REPORTS.map((r) => (
            <button key={r.id} className={`report-tile tone-${r.tone} ${id === r.id ? "active" : ""}`} onClick={() => choose(r.id)}>
              <span className="qi"><Icon name={r.icon} size={21} /></span>
              <span className="rt-title">{r.title}</span>
              <span className="rt-desc">{r.desc}</span>
            </button>
          ))}
        </div>
        {id === "year" && <YearReport />}
        {id === "month" && <MonthReport />}
        {id === "cash" && <CashReport />}
        {id === "due" && <DueReport />}
        {id === "sales" && <SalesReport />}
        {id === "halls" && <HallReport />}
      </div>
    </div>
  );
}
