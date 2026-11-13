import { useEffect, useRef, useState, type ReactNode } from "react";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { BrandMark, Flourish, Rings } from "../components/Ornament";
import { Alert, errorText, initials } from "../components/ui";
import type { LoginUserOption, ServerInfo, User } from "../types";

export function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <div className="auth">
      <div className="auth-art">
        <Rings size={360} className="auth-rings" />
        <div className="row" style={{ gap: 14 }}>
          <BrandMark size={58} />
          <span className="display" style={{ fontSize: 38, fontWeight: 700, letterSpacing: ".02em" }}>Salonera</span>
        </div>
        <div>
          <Flourish width={180} />
          <h1 style={{ marginTop: 22 }}>Mutlu günleri <em className="gold-text">birlikte</em> planlayalım.</h1>
          <p>Rezervasyonlar, müşteriler, sözleşmeler ve ödemeler tek bir yerde.</p>
        </div>
        <span style={{ color: "#b897a3", fontSize: 14 }}>Düğün salonları ve organizasyon işletmeleri için</span>
      </div>
      <div className="auth-side">{children}</div>
    </div>
  );
}

/** Giriş: kullanıcı adını yazmak yerine kendi adına tıklar, sadece parolasını yazar. */
export function Login({ info, isMain, onLogin, onChangeConnection }: {
  info: ServerInfo;
  isMain: boolean;
  onLogin: (token: string, user: User) => void;
  onChangeConnection: () => void;
}) {
  const [users, setUsers] = useState<LoginUserOption[] | null>(null);
  const [selected, setSelected] = useState<string>("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const passwordRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    api.loginUsers().then((list) => {
      setUsers(list);
      const last = localStorage.getItem("salonera-last-user");
      const preferred = list.find((u) => u.username === last) ?? (list.length === 1 ? list[0] : undefined);
      if (preferred) setSelected(preferred.username);
    }).catch((e) => setError(errorText(e)));
  }, []);

  useEffect(() => { if (selected) passwordRef.current?.focus(); }, [selected]);

  const submit = async () => {
    if (!selected || !password) return;
    setBusy(true);
    setError("");
    try {
      const response = await api.login(selected, password);
      localStorage.setItem("salonera-last-user", selected);
      onLogin(response.token, response.user);
    } catch (e) {
      setError(errorText(e));
      setPassword("");
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <div className="auth-card stack">
        <div>
          <h2 className="serif" style={{ fontSize: 32, fontWeight: 400 }}>{info.businessName}</h2>
          <p className="muted" style={{ fontSize: 17, marginTop: 6 }}>Adınıza tıklayın ve parolanızı yazın.</p>
        </div>
        {users === null && !error ? <div className="spinner" /> : (
          <div className="user-tiles">
            {(users ?? []).map((u) => (
              <button key={u.username} className={`user-tile ${selected === u.username ? "selected" : ""}`}
                onClick={() => { setSelected(u.username); setError(""); }}>
                <div className="avatar">{initials(u.name)}</div>
                <span style={{ textAlign: "center" }}>{u.name}</span>
              </button>
            ))}
          </div>
        )}
        {selected && (
          <form className="stack" style={{ gap: 14 }} onSubmit={(e) => { e.preventDefault(); void submit(); }}>
            <div className="field">
              <label>Parola</label>
              <div className="input-wrap">
                <span className="prefix"><Icon name="lock" size={18} /></span>
                <input ref={passwordRef} className="input with-prefix" type="password" value={password}
                  onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
              </div>
            </div>
            {error && <Alert kind="danger">{error}</Alert>}
            <button className="btn btn-primary btn-block" style={{ minHeight: 56, fontSize: 18 }} disabled={busy || !password}>
              Giriş Yap
            </button>
          </form>
        )}
        {!selected && error && <Alert kind="danger">{error}</Alert>}
        <p className="muted small" style={{ textAlign: "center" }}>
          Parolanızı unuttuysanız yöneticinize başvurun. Yönetici, Ayarlar → Kullanıcılar bölümünden yeni parola verebilir.
        </p>
        {!isMain && (
          <button className="btn btn-ghost" onClick={onChangeConnection} style={{ alignSelf: "center" }}>
            Başka bir ana bilgisayara bağlan
          </button>
        )}
      </div>
    </AuthLayout>
  );
}
