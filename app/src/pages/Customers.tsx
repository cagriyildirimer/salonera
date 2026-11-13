import { useEffect, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { Empty, Field, hue, initials, LoadError, Loading, Modal, useAction, useLoad } from "../components/ui";
import type { CustomerInput } from "../types";

export function Customers() {
  const { navigate } = useSession();
  const [query, setQuery] = useState("");
  const [debounced, setDebounced] = useState("");
  const [adding, setAdding] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(query), 250);
    return () => clearTimeout(t);
  }, [query]);
  const { data, error, loading, reload } = useLoad(() => api.customers(debounced), [debounced]);

  return (
    <div className="content">
      <div className="content-wide stack">
        <div className="row search-bar">
          <div className="input-wrap search-big" style={{ flex: 1, minWidth: 560, maxWidth: 720 }}>
            <span className="prefix"><Icon name="search" size={19} /></span>
            <input className="input with-prefix" autoFocus placeholder="Ad, soyad, gelin/damat adı veya telefon ile arayın"
              value={query} onChange={(e) => setQuery(e.target.value)} />
          </div>
          <span className="spacer" />
          <button className="btn btn-primary" onClick={() => setAdding(true)}><Icon name="userPlus" /> Yeni Müşteri</button>
        </div>
        {error && <LoadError message={error} onRetry={reload} />}
        <div className="card" style={{ padding: 8 }}>
          {loading && !data ? <Loading /> : !data || data.length === 0 ? (
            <Empty icon="users">{debounced ? "Aramanıza uyan müşteri bulunamadı." : "Henüz müşteri yok. İlk müşteri, ilk rezervasyonla birlikte eklenir."}</Empty>
          ) : (
            <table className="table">
              <thead><tr><th>Ad soyad</th><th>Telefon</th><th>Gelin & damat</th><th className="num">Rezervasyon</th></tr></thead>
              <tbody>
                {data.map((c) => (
                  <tr key={c.id} className="clickable" onClick={() => navigate({ name: "customer", id: c.id })}>
                    <td>
                      <div className="row" style={{ gap: 12, flexWrap: "nowrap" }}>
                        <span className={`avatar avatar-sm hue-${hue(c.fullName)}`}>{initials(c.fullName)}</span>
                        <span className="strong">{c.fullName}</span>
                      </div>
                    </td>
                    <td><span className="row" style={{ gap: 6 }}><Icon name="phone" size={15} />{c.phone}</span></td>
                    <td className="muted">{(c.brideName || c.groomName) ? <><Icon name="heart" size={14} /> {[c.brideName, c.groomName].filter(Boolean).join(" & ")}</> : "—"}</td>
                    <td className="num"><span className="count-pill">{c.reservationCount}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
      {adding && (
        <CustomerForm title="Yeni müşteri" onClose={() => setAdding(false)}
          onSave={async (input) => {
            const created = await api.createCustomer(input);
            navigate({ name: "customer", id: created.id });
          }} />
      )}
    </div>
  );
}

export function CustomerForm({ title, initial, onClose, onSave }: {
  title: string; initial?: CustomerInput; onClose: () => void; onSave: (input: CustomerInput) => Promise<void>;
}) {
  const [form, setForm] = useState<CustomerInput>(initial ?? { fullName: "", phone: "", phone2: "", brideName: "", groomName: "", notes: "" });
  const { run, busy } = useAction();
  const set = (key: keyof CustomerInput) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value });
  return (
    <Modal onClose={onClose} wide>
      <h2>{title}</h2>
      <div className="grid grid-2" style={{ marginTop: 16, gap: 16 }}>
        <Field label="Adı soyadı *"><input className="input" autoFocus value={form.fullName} onChange={set("fullName")} /></Field>
        <Field label="Telefon *" hint="Örnek: 0532 123 45 67"><input className="input" inputMode="tel" value={form.phone} onChange={set("phone")} /></Field>
        <Field label="İkinci telefon"><input className="input" inputMode="tel" value={form.phone2 ?? ""} onChange={set("phone2")} /></Field>
        <div />
        <Field label="Gelinin adı"><input className="input" value={form.brideName ?? ""} onChange={set("brideName")} /></Field>
        <Field label="Damadın adı"><input className="input" value={form.groomName ?? ""} onChange={set("groomName")} /></Field>
      </div>
      <div style={{ marginTop: 16 }}>
        <Field label="Notlar"><textarea className="textarea" value={form.notes ?? ""} onChange={set("notes")} /></Field>
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy} onClick={() => run(() => onSave(form), "Müşteri kaydedildi.")}>Kaydet</button>
      </div>
    </Modal>
  );
}
