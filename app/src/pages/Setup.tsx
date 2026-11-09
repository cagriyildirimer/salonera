import { useState } from "react";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { Alert, errorText, Field, NumberInput } from "../components/ui";
import { AuthLayout } from "./Login";

/** Ana bilgisayarda ilk kurulum: işletme, yönetici hesabı ve salonlar. */
export function Setup({ onDone }: { onDone: () => void }) {
  const [business, setBusiness] = useState("");
  const [phone, setPhone] = useState("");
  const [adminName, setAdminName] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [password2, setPassword2] = useState("");
  const [halls, setHalls] = useState([{ name: "", capacity: 0 }]);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  const suggestUsername = (name: string) =>
    name.toLocaleLowerCase("tr-TR").split(/\s+/)[0]
      .replace(/ç/g, "c").replace(/ğ/g, "g").replace(/ı/g, "i").replace(/ö/g, "o").replace(/ş/g, "s").replace(/ü/g, "u")
      .replace(/[^a-z0-9]/g, "");

  const submit = async () => {
    setError("");
    if (password !== password2) {
      setError("İki parola birbirinin aynısı değil.");
      return;
    }
    setBusy(true);
    try {
      await api.setup({
        businessName: business,
        phone,
        adminName,
        adminUsername: username,
        adminPassword: password,
        halls: halls.filter((h) => h.name.trim()),
      });
      onDone();
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <div className="auth-card stack" style={{ width: "min(560px, 100%)" }}>
        <div>
          <h2 className="serif" style={{ fontSize: 30, fontWeight: 400 }}>İşletmenizi tanıtın</h2>
          <p className="muted" style={{ fontSize: 17, marginTop: 6 }}>Bu bilgiler bir kez sorulur, sonra Ayarlar'dan değiştirilebilir.</p>
        </div>

        <div className="grid grid-2">
          <Field label="İşletme adı"><input className="input" value={business} onChange={(e) => setBusiness(e.target.value)} placeholder="Kristal Düğün Salonu" autoFocus /></Field>
          <Field label="Telefon"><input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="0212 555 00 00" /></Field>
        </div>

        <div className="hr" style={{ margin: 0 }} />
        <div className="label">Yönetici hesabı (sizin hesabınız)</div>
        <div className="grid grid-2">
          <Field label="Adınız soyadınız">
            <input className="input" value={adminName} onChange={(e) => {
              setAdminName(e.target.value);
              if (!username || username === suggestUsername(adminName)) setUsername(suggestUsername(e.target.value));
            }} />
          </Field>
          <Field label="Kullanıcı adı"><input className="input" value={username} onChange={(e) => setUsername(e.target.value)} /></Field>
          <Field label="Parola" hint="En az 8 karakter, harf ve rakam içersin.">
            <input className="input" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          <Field label="Parola (tekrar)"><input className="input" type="password" value={password2} onChange={(e) => setPassword2(e.target.value)} /></Field>
        </div>

        <div className="hr" style={{ margin: 0 }} />
        <div className="label">Salonlarınız</div>
        {halls.map((hall, index) => (
          <div key={index} className="row" style={{ flexWrap: "nowrap", alignItems: "flex-end" }}>
            <div style={{ flex: 2 }}>
              <Field label={`${index + 1}. salonun adı`}>
                <input className="input" value={hall.name} placeholder={index === 0 ? "Kristal Salon" : "Bahçe"}
                  onChange={(e) => setHalls(halls.map((h, i) => (i === index ? { ...h, name: e.target.value } : h)))} />
              </Field>
            </div>
            <div style={{ flex: 1 }}>
              <Field label="Kaç kişilik?">
                <NumberInput value={hall.capacity} onChange={(v) => setHalls(halls.map((h, i) => (i === index ? { ...h, capacity: v } : h)))} />
              </Field>
            </div>
            {halls.length > 1 && (
              <button className="btn icon-btn" title="Kaldır" onClick={() => setHalls(halls.filter((_, i) => i !== index))}>
                <Icon name="x" size={18} />
              </button>
            )}
          </div>
        ))}
        <button className="btn btn-ghost" style={{ alignSelf: "flex-start" }} onClick={() => setHalls([...halls, { name: "", capacity: 0 }])}>
          <Icon name="plus" size={18} /> Salon ekle
        </button>

        {error && <Alert kind="danger">{error}</Alert>}
        <button className="btn btn-primary btn-block" style={{ minHeight: 56 }} disabled={busy} onClick={submit}>
          Kurulumu Tamamla
        </button>
      </div>
    </AuthLayout>
  );
}
