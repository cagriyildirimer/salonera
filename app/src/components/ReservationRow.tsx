import { useSession } from "../App";
import { kindLabels, tl, weekday } from "../format";
import type { ReservationSummary } from "../types";
import { DateBox, StatusBadge } from "./ui";

/** Listelerde bir rezervasyon satırı: tarih kutusu, müşteri, saat/salon, durum. Tıklayınca kart açılır. */
export function ReservationRow({ r, extra }: { r: ReservationSummary; extra?: "remaining" | "overdue" | "option" }) {
  const { navigate } = useSession();
  return (
    <button className="list-item" onClick={() => navigate({ name: "reservation", id: r.id })}>
      <DateBox date={r.date} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div className="item-title" style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{r.customerName}</div>
        <div className="item-sub">
          {weekday(r.date)} · {r.start}–{r.end} · {r.hallName} · {kindLabels[r.kind]} · {r.guestCount} kişi
        </div>
      </div>
      {extra === "overdue" && r.overdue > 0 ? (
        <div style={{ textAlign: "right" }}>
          <div className="strong" style={{ color: "var(--danger)", fontSize: 17 }}>{tl(r.overdue, true)}</div>
          <div className="item-sub">gecikmiş</div>
        </div>
      ) : extra === "remaining" && r.remaining > 0 ? (
        <div style={{ textAlign: "right" }}>
          <div className="strong" style={{ fontSize: 17 }}>{tl(r.remaining, true)}</div>
          <div className="item-sub">kalan</div>
        </div>
      ) : extra === "option" && r.optionUntil ? (
        <div style={{ textAlign: "right" }}>
          <StatusBadge r={r} />
        </div>
      ) : (
        <StatusBadge r={r} />
      )}
    </button>
  );
}
