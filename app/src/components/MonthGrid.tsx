import { addDays, isoDate, monthName, parseDate, today } from "../format";
import type { ReservationSummary } from "../types";
import { Icon } from "./Icon";

const DOW = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];

/** Ayın ilk gününden başlayan, Pazartesi ile açılan 6 haftalık ızgaranın ilk ve son günü (son gün hariç). */
export function gridRange(month: string): { from: string; to: string } {
  const first = parseDate(month);
  const offset = (first.getDay() + 6) % 7;
  const from = addDays(isoDate(first), -offset);
  return { from, to: addDays(from, 42) };
}

export function monthOf(date: string) {
  return `${date.slice(0, 7)}-01`;
}

export function shiftMonth(month: string, delta: number) {
  const d = parseDate(month);
  return isoDate(new Date(d.getFullYear(), d.getMonth() + delta, 1));
}

/** Aylık takvim ızgarası. Her günde o günün kayıtları (salon · müşteri) gösterilir. */
export function MonthGrid({ month, events, selected, hallCount, onSelect, onOpen, onMonth, compact, disablePast }: {
  month: string;
  events: ReservationSummary[];
  selected?: string | null;
  hallCount: number;
  onSelect: (date: string) => void;
  onOpen?: (id: string) => void;
  onMonth: (month: string) => void;
  compact?: boolean;
  disablePast?: boolean;
}) {
  const { from } = gridRange(month);
  const now = today();
  const m = parseDate(month);
  const days = Array.from({ length: 42 }, (_, i) => addDays(from, i));
  const byDay = new Map<string, ReservationSummary[]>();
  for (const e of events) {
    const list = byDay.get(e.date) ?? [];
    list.push(e);
    byDay.set(e.date, list);
  }
  const limit = compact ? 0 : 3;

  return (
    <div className={compact ? "mini-cal" : undefined}>
      <div className="cal-head">
        <button className="btn icon-btn" title="Önceki ay" onClick={() => onMonth(shiftMonth(month, -1))}><Icon name="left" /></button>
        <div className="cal-title">{monthName(m.getMonth() + 1)}<span className="yr">{m.getFullYear()}</span></div>
        <button className="btn icon-btn" title="Sonraki ay" onClick={() => onMonth(shiftMonth(month, 1))}><Icon name="right" /></button>
        <span className="spacer" />
        <button className="btn" onClick={() => { onMonth(monthOf(now)); onSelect(now); }}>Bugün</button>
      </div>
      <div className="cal-grid">
        {DOW.map((d, i) => <div key={d} className={`cal-dow ${i >= 5 ? "we" : ""}`}>{d}</div>)}
        {days.map((day) => {
          const list = byDay.get(day) ?? [];
          const busyHalls = new Set(list.map((e) => e.hallId)).size;
          const full = hallCount > 0 && busyHalls >= hallCount;
          const past = day < now;
          const classes = [
            "cal-day",
            day.slice(0, 7) !== month.slice(0, 7) && "other",
            past && "past",
            day === now && "today",
            parseDate(day).getDay() % 6 === 0 && "weekend",
            full && "full",
            day === selected && "selected",
          ].filter(Boolean).join(" ");
          return (
            <button key={day} className={classes} disabled={disablePast && past} onClick={() => onSelect(day)}
              title={list.map((e) => `${e.hallName}: ${e.customerName} (${e.start}–${e.end})`).join("\n")}>
              <span className="cal-num">{parseDate(day).getDate()}</span>
              {compact ? (
                list.length > 0 && (
                  <span className="cal-more" style={{ color: full ? "var(--danger)" : "var(--warning)", fontWeight: 600 }}>
                    {full ? "Dolu" : `${list.length} kayıt`}
                  </span>
                )
              ) : (
                <>
                  {list.slice(0, limit).map((e) => (
                    <span key={e.id} className={`cal-ev ${e.status === "on_rezervasyon" ? "option" : ""}`}
                      onClick={(ev) => { if (onOpen) { ev.stopPropagation(); onOpen(e.id); } }}>
                      {e.customerName}
                    </span>
                  ))}
                  {list.length > limit && <span className="cal-more">+{list.length - limit} kayıt daha</span>}
                </>
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
