import { useEffect, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon, type IconName } from "../components/Icon";
import { ReservationRow } from "../components/ReservationRow";
import { Empty, LoadError, Loading, useLoad } from "../components/ui";
import { addDays, dateLong, dateTime, isoDate, methodLabels, parseDate, tl, today } from "../format";
import type { Method, ReservationSummary } from "../types";

type Range = "today" | "week" | "month";

const METHOD_TONE: Record<Method, "wine" | "plum" | "green"> = { nakit: "green", kredi_karti: "plum", havale: "wine" };
const METHOD_ICON: Record<Method, IconName> = { nakit: "money", kredi_karti: "wallet", havale: "building" };

function rangeOf(range: Range): { from: string; to: string; label: string } {
  const now = today();
  if (range === "today") return { from: now, to: now, label: dateLong(now) };
  if (range === "week") {
    const d = parseDate(now);
    const monday = addDays(now, -((d.getDay() + 6) % 7));
    return { from: monday, to: now, label: `${dateLong(monday)} – ${dateLong(now)}` };
  }
  const first = isoDate(new Date(parseDate(now).getFullYear(), parseDate(now).getMonth(), 1));
  return { from: first, to: now, label: `${dateLong(first)} – ${dateLong(now)}` };
}

export function Payments() {
  const { navigate } = useSession();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ReservationSummary[] | null>(null);
  const [range, setRange] = useState<Range>("today");
  const { from, to, label } = rangeOf(range);
  const list = useLoad(() => api.payments(from, to), [from, to]);

  useEffect(() => {
    if (query.trim().length < 2) {
      setResults(null);
      return;
    }
    let alive = true;
    const t = setTimeout(() => {
      api.searchReservations(query).then((r) => alive && setResults(r.filter((x) => x.status === "kesin"))).catch(() => alive && setResults([]));
    }, 250);
    return () => { alive = false; clearTimeout(t); };
  }, [query]);

  const active = (list.data ?? []).filter((p) => !p.isCancelled);
  const byMethod = (["nakit", "kredi_karti", "havale"] as Method[]).map((m) => ({
    method: m,
    total: active.filter((p) => p.method === m).reduce((s, p) => s + (p.isRefund ? -p.amount : p.amount), 0),
  }));
  const net = byMethod.reduce((s, x) => s + x.total, 0);

  return (
    <div className="content">
      <div className="content-wide stack">
        <div className="card quick">
          <div className="card-title">Ödeme al</div>
          <div className="card-sub" style={{ marginBottom: 14 }}>Müşterinin adını veya telefonunu yazın, rezervasyonuna tıklayıp “Ödeme Al”a basın.</div>
          <div className="input-wrap search-big" style={{ maxWidth: 620 }}>
            <span className="prefix"><Icon name="search" size={19} /></span>
            <input className="input with-prefix" autoFocus placeholder="Müşteri adı, telefon veya kayıt numarası" value={query} onChange={(e) => setQuery(e.target.value)} />
          </div>
          {results && (
            <div className="list" style={{ marginTop: 10 }}>
              {results.length === 0 ? <Empty icon="search">Aramaya uyan kesin kayıt bulunamadı.</Empty>
                : results.map((r) => <ReservationRow key={r.id} r={r} extra="remaining" />)}
            </div>
          )}
        </div>

        <div className="card">
          <div className="card-head">
            <div>
              <div className="card-title">Alınan ödemeler</div>
              <div className="card-sub">{label}</div>
            </div>
            <div className="segment">
              <button className={range === "today" ? "on" : ""} onClick={() => setRange("today")}>Bugün</button>
              <button className={range === "week" ? "on" : ""} onClick={() => setRange("week")}>Bu hafta</button>
              <button className={range === "month" ? "on" : ""} onClick={() => setRange("month")}>Bu ay</button>
            </div>
          </div>
          <div className="grid grid-4" style={{ marginBottom: 18 }}>
            {byMethod.map((m) => (
              <div key={m.method} className={`pay-tile tone-${METHOD_TONE[m.method]}`}>
                <span className="qi"><Icon name={METHOD_ICON[m.method]} size={20} /></span>
                <div>
                  <div className="k">{methodLabels[m.method]}</div>
                  <div className="v">{tl(m.total, true)}</div>
                </div>
              </div>
            ))}
            <div className="pay-tile pay-total">
              <span className="qi"><Icon name="check" size={20} /></span>
              <div>
                <div className="k">Toplam</div>
                <div className="v">{tl(net, true)}</div>
              </div>
            </div>
          </div>
          {list.error && <LoadError message={list.error} onRetry={list.reload} />}
          {list.loading && !list.data ? <Loading /> : (list.data ?? []).length === 0 ? <Empty icon="wallet">Bu dönemde ödeme alınmadı.</Empty> : (
            <table className="table">
              <thead><tr><th>Tarih</th><th>Müşteri</th><th>Makbuz</th><th>Şekli</th><th>Alan</th><th className="num">Tutar</th></tr></thead>
              <tbody>
                {list.data!.map((p) => (
                  <tr key={p.id} className={`clickable ${p.isCancelled ? "cancelled" : ""}`} onClick={() => navigate({ name: "reservation", id: p.reservationId })}>
                    <td>{dateTime(p.date)}</td>
                    <td className="strong">{p.customerName}</td>
                    <td>{p.no}{p.isRefund && <span className="badge badge-warning" style={{ marginLeft: 8 }}>İade</span>}</td>
                    <td><span className={`method m-${p.method}`}>{methodLabels[p.method]}</span></td>
                    <td className="muted">{p.createdBy}</td>
                    <td className="num strong" style={{ color: p.isRefund ? "var(--danger)" : undefined }}>{p.isRefund ? "− " : ""}{tl(p.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
}
