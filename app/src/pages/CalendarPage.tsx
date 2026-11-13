import { useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { gridRange, monthOf, MonthGrid } from "../components/MonthGrid";
import { DateBox, LoadError, StatusBadge, useLoad } from "../components/ui";
import { dateWithDay, kindLabels, today } from "../format";

export function CalendarPage({ month: initialMonth }: { month?: string }) {
  const { navigate } = useSession();
  const [month, setMonth] = useState(initialMonth ?? monthOf(today()));
  const [selected, setSelected] = useState<string>(today());
  const halls = useLoad(() => api.halls());
  const events = useLoad(() => {
    const { from, to } = gridRange(month);
    return api.calendar(from, to);
  }, [month]);

  const dayEvents = (events.data ?? []).filter((e) => e.date === selected);
  const isPast = selected < today();

  return (
    <div className="content">
      <div className="content-wide">
        {events.error && <LoadError message={events.error} onRetry={events.reload} />}
        <div className="split" style={{ gridTemplateColumns: "minmax(0, 7fr) minmax(320px, 3fr)" }}>
          <div className="card">
            <MonthGrid month={month} events={events.data ?? []} selected={selected} hallCount={halls.data?.length ?? 0}
              onSelect={(d) => { setSelected(d); if (monthOf(d) !== month && d.slice(0, 7) !== month.slice(0, 7)) setMonth(monthOf(d)); }}
              onOpen={(id) => navigate({ name: "reservation", id })} onMonth={setMonth} />
            <div className="legend" style={{ marginTop: 16 }}>
              <span><i className="dot" style={{ background: "var(--wine-grad)" }} /> Kesin kayıt</span>
              <span><i className="dot" style={{ background: "linear-gradient(135deg, #f3d495, #e2b55e)" }} /> Ön rezervasyon</span>
              <span><i className="dot" style={{ background: "#f3c9c4" }} /> Bütün salonlar dolu</span>
            </div>
          </div>

          <div className="card" style={{ position: "sticky", top: 0 }}>
            <div className="day-head">
              <DateBox date={selected} />
              <div>
                <div className="card-title">{dateWithDay(selected)}</div>
                <div className="card-sub">Salonların durumu</div>
              </div>
            </div>
            <div className="stack" style={{ gap: 10 }}>
              {(halls.data ?? []).map((hall) => {
                const busy = dayEvents.filter((e) => e.hallId === hall.id);
                return (
                  <div key={hall.id} className={`hall-box ${busy.length ? "busy" : "free"}`}>
                    <div className="row">
                      <span className="strong" style={{ fontSize: 17 }}>{hall.name}</span>
                      <span className="muted small">{hall.capacity} kişilik</span>
                      <span className="spacer" />
                      {busy.length === 0 && <span className="badge badge-success">Boş</span>}
                    </div>
                    {busy.map((e) => (
                      <button key={e.id} className="list-item" style={{ padding: "10px 4px" }}
                        onClick={() => navigate({ name: "reservation", id: e.id })}>
                        <div style={{ flex: 1 }}>
                          <div className="strong">{e.customerName}</div>
                          <div className="item-sub">{e.start}–{e.end} · {kindLabels[e.kind]} · {e.guestCount} kişi</div>
                        </div>
                        <StatusBadge r={e} />
                      </button>
                    ))}
                    {busy.length === 0 && !isPast && (
                      <button className="btn btn-ghost" style={{ marginTop: 6, paddingLeft: 0 }}
                        onClick={() => navigate({ name: "new", date: selected, hallId: hall.id })}>
                        <Icon name="plus" size={18} /> Bu salona rezervasyon yap
                      </button>
                    )}
                  </div>
                );
              })}
            </div>
            {!isPast && (
              <button className="btn btn-primary btn-block" style={{ marginTop: 18 }} onClick={() => navigate({ name: "new", date: selected })}>
                <Icon name="plus" /> Bu güne rezervasyon yap
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
