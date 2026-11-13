import { useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { ReservationRow } from "../components/ReservationRow";
import { Empty, initials, LoadError, Loading, useAction, useFeedback, useLoad } from "../components/ui";
import { telHref, tl } from "../format";
import { CustomerForm } from "./Customers";

export function CustomerPage({ id }: { id: string }) {
  const { navigate, back } = useSession();
  const { confirm } = useFeedback();
  const { run } = useAction();
  const { data, error, loading, reload } = useLoad(() => api.customer(id), [id]);
  const [editing, setEditing] = useState(false);

  if (loading && !data) return <div className="content"><Loading /></div>;
  if (error || !data) return <div className="content"><LoadError message={error ?? ""} onRetry={reload} /></div>;
  const c = data.customer;
  const active = data.reservations.filter((r) => r.status !== "iptal");
  const remaining = active.reduce((s, r) => s + r.remaining, 0);

  const remove = async () => {
    const ok = await confirm({
      title: "Müşteri listeden kaldırılsın mı?",
      message: `${c.fullName} müşteri listesinden kaldırılacak. Geçmiş rezervasyon ve ödeme kayıtları silinmez.`,
      confirmText: "Evet, kaldır",
      danger: true,
    });
    if (ok && (await run(() => api.deleteCustomer(id), "Müşteri listeden kaldırıldı."))) back();
  };

  return (
    <div className="content">
      <div className="content-wide stack">
        <div className="card res-card">
          <div className="res-banner">
          <div className="row" style={{ gap: 20 }}>
            <div className="avatar avatar-xl">{initials(c.fullName)}</div>
            <div style={{ flex: 1 }}>
              <h2 className="display" style={{ fontSize: 38, fontWeight: 700, lineHeight: 1.1 }}>{c.fullName}</h2>
              <div className="row" style={{ gap: 18, marginTop: 4, fontSize: 17 }}>
                <a href={telHref(c.phone)} className="row" style={{ gap: 6 }}><Icon name="phone" size={17} /> {c.phone}</a>
                {c.phone2 && <a href={telHref(c.phone2)}>{c.phone2}</a>}
                {(c.brideName || c.groomName) && <span className="muted"><Icon name="heart" size={16} /> {[c.brideName, c.groomName].filter(Boolean).join(" & ")}</span>}
              </div>
            </div>
            <div className="profile-stats">
              <div><b>{data.reservations.length}</b><span>kayıt</span></div>
              <div><b>{remaining > 0 ? tl(remaining, true) : "—"}</b><span>kalan borç</span></div>
            </div>
          </div>
          </div>
          <div className="res-body">
            <div className="row">
              <button className="btn btn-primary" onClick={() => navigate({ name: "new", customerId: id })}><Icon name="plus" /> Yeni Rezervasyon</button>
              <button className="btn" onClick={() => setEditing(true)}><Icon name="edit" /> Düzenle</button>
            </div>
            {c.notes && <div style={{ marginTop: 16, whiteSpace: "pre-wrap" }} className="alert alert-info">{c.notes}</div>}
          </div>
        </div>

        <div className="card">
          <div className="card-head">
            <div>
              <div className="card-title">Rezervasyonları</div>
              <div className="card-sub">{active.length} aktif kayıt{remaining > 0 ? ` · toplam kalan borç ${tl(remaining)}` : ""}</div>
            </div>
          </div>
          {data.reservations.length === 0 ? <Empty icon="calendar">Bu müşterinin henüz rezervasyonu yok.</Empty> : (
            <div className="list">{data.reservations.map((r) => <ReservationRow key={r.id} r={r} extra="remaining" />)}</div>
          )}
        </div>

        <div className="row">
          <span className="spacer" />
          <button className="btn btn-danger" onClick={remove}><Icon name="trash" /> Müşteriyi listeden kaldır</button>
        </div>
      </div>
      {editing && (
        <CustomerForm title="Müşteri bilgilerini düzenle" initial={c} onClose={() => setEditing(false)}
          onSave={async (input) => { await api.updateCustomer(id, input); setEditing(false); await reload(); }} />
      )}
    </div>
  );
}
