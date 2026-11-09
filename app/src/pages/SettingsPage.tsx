import { useEffect, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon, type IconName } from "../components/Icon";
import { Alert, Empty, Field, LoadError, Loading, Modal, MoneyInput, NumberInput, useAction, useFeedback, useLoad } from "../components/ui";
import { dateTime, roleLabels, tl } from "../format";
import { shell } from "../shell";
import type { BusinessSettings, Hall, Product, Role, User } from "../types";

type Tab = "business" | "halls" | "packages" | "extras" | "users" | "password" | "system";

const TABS: { id: Tab; label: string; icon: IconName; admin?: boolean }[] = [
  { id: "business", label: "İşletme ve sözleşme", icon: "building", admin: true },
  { id: "halls", label: "Salonlar", icon: "home", admin: true },
  { id: "packages", label: "Paketler", icon: "package", admin: true },
  { id: "extras", label: "Ek hizmetler", icon: "star", admin: true },
  { id: "users", label: "Kullanıcılar", icon: "users", admin: true },
  { id: "password", label: "Parolam", icon: "lock" },
  { id: "system", label: "Bağlantı ve yedek", icon: "wifi" },
];

export function SettingsPage({ tab: initial }: { tab?: string }) {
  const { isAdmin } = useSession();
  const tabs = TABS.filter((t) => !t.admin || isAdmin);
  const [tab, setTab] = useState<Tab>((tabs.find((t) => t.id === initial)?.id ?? tabs[0].id));
  return (
    <div className="content">
      <div className="content-wide" style={{ display: "grid", gridTemplateColumns: "250px minmax(0, 1fr)", gap: 24, alignItems: "start" }}>
        <div className="card" style={{ padding: 10 }}>
          {tabs.map((t) => (
            <button key={t.id} className={`nav-item ${tab === t.id ? "active" : ""}`} style={{
              width: "100%", color: tab === t.id ? "var(--primary)" : "var(--text)",
              background: tab === t.id ? "var(--primary-soft)" : "transparent",
            }} onClick={() => setTab(t.id)}>
              <Icon name={t.icon} size={20} /> {t.label}
            </button>
          ))}
        </div>
        <div>
          {tab === "business" && <BusinessTab />}
          {tab === "halls" && <HallsTab />}
          {tab === "packages" && <ProductsTab kind="packages" />}
          {tab === "extras" && <ProductsTab kind="extras" />}
          {tab === "users" && <UsersTab />}
          {tab === "password" && <PasswordTab />}
          {tab === "system" && <SystemTab />}
        </div>
      </div>
    </div>
  );
}

function BusinessTab() {
  const { refreshInfo } = useSession();
  const { data, error, reload } = useLoad(() => api.settings());
  const [form, setForm] = useState<BusinessSettings | null>(null);
  const { run, busy } = useAction();
  useEffect(() => { if (data) setForm(data); }, [data]);
  if (error) return <LoadError message={error} onRetry={reload} />;
  if (!form) return <Loading />;
  const set = (key: keyof BusinessSettings) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value });
  return (
    <div className="card stack">
      <div><div className="card-title">İşletme bilgileri</div><div className="card-sub">Sözleşme ve makbuzların üst kısmında görünür.</div></div>
      <div className="grid grid-2">
        <Field label="İşletme adı"><input className="input" value={form.businessName} onChange={set("businessName")} /></Field>
        <Field label="Telefon"><input className="input" value={form.phone ?? ""} onChange={set("phone")} /></Field>
        <Field label="Vergi dairesi"><input className="input" value={form.taxOffice ?? ""} onChange={set("taxOffice")} /></Field>
        <Field label="Vergi numarası"><input className="input" value={form.taxNumber ?? ""} onChange={set("taxNumber")} /></Field>
      </div>
      <Field label="Adres"><textarea className="textarea" style={{ minHeight: 70 }} value={form.address ?? ""} onChange={set("address")} /></Field>
      <Field label="Ön rezervasyon süresi (gün)" hint="Yeni ön rezervasyonlarda tarihin kaç gün tutulacağı (öneri olarak gelir).">
        <div style={{ maxWidth: 160 }}><NumberInput value={form.defaultOptionDays} onChange={(v) => setForm({ ...form, defaultOptionDays: v })} /></div>
      </Field>
      <Field label="Sözleşme maddeleri" hint="Her madde ayrı satırda. Sözleşmenin altına olduğu gibi yazdırılır.">
        <textarea className="textarea" style={{ minHeight: 240 }} value={form.contractTerms} onChange={set("contractTerms")} />
      </Field>
      <div className="row row-end">
        <button className="btn btn-primary" disabled={busy} onClick={() => run(async () => { setForm(await api.saveSettings(form)); await refreshInfo(); }, "İşletme bilgileri kaydedildi.")}>
          Kaydet
        </button>
      </div>
    </div>
  );
}

function HallsTab() {
  const { data, error, loading, reload } = useLoad(() => api.halls(true));
  const [editing, setEditing] = useState<Hall | "new" | null>(null);
  return (
    <div className="card">
      <div className="card-head">
        <div><div className="card-title">Salonlar</div><div className="card-sub">Kullanımdan kaldırılan salon takvimde görünmez; eski kayıtları korunur.</div></div>
        <button className="btn btn-primary" onClick={() => setEditing("new")}><Icon name="plus" /> Salon ekle</button>
      </div>
      {error && <LoadError message={error} onRetry={reload} />}
      {loading && !data ? <Loading /> : (
        <table className="table">
          <thead><tr><th>Salon</th><th className="num">Kapasite</th><th>Durum</th><th /></tr></thead>
          <tbody>
            {(data ?? []).map((h) => (
              <tr key={h.id} className="clickable" onClick={() => setEditing(h)}>
                <td className="strong">{h.name}</td>
                <td className="num">{h.capacity} kişi</td>
                <td>{h.isActive ? <span className="badge badge-success">Kullanımda</span> : <span className="badge badge-muted">Kaldırıldı</span>}</td>
                <td className="num"><Icon name="edit" size={18} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {editing && <HallModal hall={editing === "new" ? null : editing} onClose={() => setEditing(null)} onDone={() => { setEditing(null); void reload(); }} />}
    </div>
  );
}

function HallModal({ hall, onClose, onDone }: { hall: Hall | null; onClose: () => void; onDone: () => void }) {
  const [name, setName] = useState(hall?.name ?? "");
  const [capacity, setCapacity] = useState(hall?.capacity ?? 0);
  const [active, setActive] = useState(hall?.isActive ?? true);
  const { run, busy } = useAction();
  return (
    <Modal onClose={onClose}>
      <h2>{hall ? "Salonu düzenle" : "Yeni salon"}</h2>
      <div className="stack" style={{ gap: 16, marginTop: 16 }}>
        <Field label="Salon adı"><input className="input" autoFocus value={name} onChange={(e) => setName(e.target.value)} /></Field>
        <Field label="Kaç kişilik?"><NumberInput value={capacity} onChange={setCapacity} /></Field>
        <label className="checkbox"><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /> Kullanımda</label>
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy}
          onClick={async () => { if (await run(() => api.saveHall(hall?.id ?? null, { name, capacity, isActive: active }), "Salon kaydedildi.")) onDone(); }}>Kaydet</button>
      </div>
    </Modal>
  );
}

function ProductsTab({ kind }: { kind: "packages" | "extras" }) {
  const isPackage = kind === "packages";
  const { data, error, loading, reload } = useLoad(() => (isPackage ? api.packages(true) : api.extras(true)), [kind]);
  const [editing, setEditing] = useState<Product | "new" | null>(null);
  return (
    <div className="card">
      <div className="card-head">
        <div>
          <div className="card-title">{isPackage ? "Paketler" : "Ek hizmetler"}</div>
          <div className="card-sub">Fiyat değişikliği yalnızca yeni satışları etkiler; yapılmış sözleşmeler değişmez.</div>
        </div>
        <button className="btn btn-primary" onClick={() => setEditing("new")}><Icon name="plus" /> {isPackage ? "Paket ekle" : "Hizmet ekle"}</button>
      </div>
      {error && <LoadError message={error} onRetry={reload} />}
      {loading && !data ? <Loading /> : (data ?? []).length === 0 ? (
        <Empty icon="package">{isPackage ? "Henüz paket yok. Kesin kayıt yapabilmek için en az bir paket ekleyin." : "Henüz ek hizmet yok (isteğe bağlı)."}</Empty>
      ) : (
        <table className="table">
          <thead><tr><th>Ad</th><th className="num">Fiyat</th><th>Durum</th><th /></tr></thead>
          <tbody>
            {data!.map((p) => (
              <tr key={p.id} className="clickable" onClick={() => setEditing(p)}>
                <td><span className="strong">{p.name}</span>{p.description && <div className="muted small">{p.description}</div>}</td>
                <td className="num strong">{tl(p.price)}</td>
                <td>{p.isActive ? <span className="badge badge-success">Satışta</span> : <span className="badge badge-muted">Satıştan kaldırıldı</span>}</td>
                <td className="num"><Icon name="edit" size={18} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {editing && <ProductModal kind={kind} product={editing === "new" ? null : editing} onClose={() => setEditing(null)} onDone={() => { setEditing(null); void reload(); }} />}
    </div>
  );
}

function ProductModal({ kind, product, onClose, onDone }: { kind: "packages" | "extras"; product: Product | null; onClose: () => void; onDone: () => void }) {
  const isPackage = kind === "packages";
  const [name, setName] = useState(product?.name ?? "");
  const [description, setDescription] = useState(product?.description ?? "");
  const [price, setPrice] = useState(product?.price ?? 0);
  const [active, setActive] = useState(product?.isActive ?? true);
  const { run, busy } = useAction();
  const save = () => run(() => {
    const body = { name, description: description || null, price, isActive: active };
    return isPackage ? api.savePackage(product?.id ?? null, body) : api.saveExtra(product?.id ?? null, body);
  }, "Kaydedildi.");
  return (
    <Modal onClose={onClose}>
      <h2>{product ? "Düzenle" : isPackage ? "Yeni paket" : "Yeni ek hizmet"}</h2>
      <div className="stack" style={{ gap: 16, marginTop: 16 }}>
        <Field label="Adı"><input className="input" autoFocus value={name} onChange={(e) => setName(e.target.value)}
          placeholder={isPackage ? "Altın Paket" : "Fotoğraf ve video"} /></Field>
        {isPackage && (
          <Field label="Neler dahil? (isteğe bağlı)" hint="Paket seçerken ve sözleşmede görünür.">
            <textarea className="textarea" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Yemek, pasta, DJ, süsleme…" />
          </Field>
        )}
        <Field label="Fiyatı (KDV dahil)"><MoneyInput value={price} onChange={setPrice} /></Field>
        <label className="checkbox"><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /> Satışta</label>
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy} onClick={async () => { if (await save()) onDone(); }}>Kaydet</button>
      </div>
    </Modal>
  );
}

function UsersTab() {
  const { data, error, loading, reload } = useLoad(() => api.users());
  const [editing, setEditing] = useState<User | "new" | null>(null);
  return (
    <div className="card">
      <div className="card-head">
        <div>
          <div className="card-title">Kullanıcılar</div>
          <div className="card-sub">Çalışan: rezervasyon yapar ve ödeme alır. Yönetici: ek olarak ayarları, raporları ve ödeme iptalini yönetir.</div>
        </div>
        <button className="btn btn-primary" onClick={() => setEditing("new")}><Icon name="userPlus" /> Kullanıcı ekle</button>
      </div>
      {error && <LoadError message={error} onRetry={reload} />}
      {loading && !data ? <Loading /> : (
        <table className="table">
          <thead><tr><th>Ad soyad</th><th>Kullanıcı adı</th><th>Yetki</th><th>Durum</th><th /></tr></thead>
          <tbody>
            {(data ?? []).map((u) => (
              <tr key={u.id} className="clickable" onClick={() => setEditing(u)}>
                <td className="strong">{u.name}</td>
                <td className="muted">{u.username}</td>
                <td>{roleLabels[u.role]}</td>
                <td>{u.isActive ? <span className="badge badge-success">Aktif</span> : <span className="badge badge-muted">Pasif</span>}</td>
                <td className="num"><Icon name="edit" size={18} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {editing && <UserModal user={editing === "new" ? null : editing} onClose={() => setEditing(null)} onDone={() => { setEditing(null); void reload(); }} />}
    </div>
  );
}

function UserModal({ user, onClose, onDone }: { user: User | null; onClose: () => void; onDone: () => void }) {
  const [name, setName] = useState(user?.name ?? "");
  const [username, setUsername] = useState(user?.username ?? "");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<Role>(user?.role ?? "calisan");
  const [active, setActive] = useState(user?.isActive ?? true);
  const { run, busy } = useAction();
  return (
    <Modal onClose={onClose}>
      <h2>{user ? "Kullanıcıyı düzenle" : "Yeni kullanıcı"}</h2>
      <div className="stack" style={{ gap: 16, marginTop: 16 }}>
        <Field label="Ad soyad"><input className="input" autoFocus value={name} onChange={(e) => setName(e.target.value)} /></Field>
        <Field label="Kullanıcı adı" hint="Türkçe karakter ve boşluk olmadan (örnek: mehmet)."><input className="input" value={username} onChange={(e) => setUsername(e.target.value)} /></Field>
        <Field label={user ? "Yeni parola (değişmeyecekse boş bırakın)" : "Parola"} hint="En az 8 karakter, harf ve rakam içersin.">
          <input className="input" type="text" value={password} onChange={(e) => setPassword(e.target.value)} />
        </Field>
        <div className="field">
          <label>Yetki</label>
          <div className="segment">
            <button className={role === "calisan" ? "on" : ""} onClick={() => setRole("calisan")}>Çalışan</button>
            <button className={role === "yonetici" ? "on" : ""} onClick={() => setRole("yonetici")}>Yönetici</button>
          </div>
        </div>
        <label className="checkbox"><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} /> Aktif (giriş yapabilir)</label>
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy}
          onClick={async () => { if (await run(() => api.saveUser(user?.id ?? null, { name, username, role, isActive: active, password: password || undefined }), "Kullanıcı kaydedildi.")) onDone(); }}>
          Kaydet
        </button>
      </div>
    </Modal>
  );
}

function PasswordTab() {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [again, setAgain] = useState("");
  const { run, busy } = useAction();
  const mismatch = again !== "" && next !== again;
  return (
    <div className="card stack" style={{ maxWidth: 520 }}>
      <div className="card-title">Parolamı değiştir</div>
      <Field label="Şu anki parolanız"><input className="input" type="password" value={current} onChange={(e) => setCurrent(e.target.value)} /></Field>
      <Field label="Yeni parola" hint="En az 8 karakter, harf ve rakam içersin."><input className="input" type="password" value={next} onChange={(e) => setNext(e.target.value)} /></Field>
      <Field label="Yeni parola (tekrar)"><input className="input" type="password" value={again} onChange={(e) => setAgain(e.target.value)} /></Field>
      {mismatch && <div className="form-error">İki parola aynı değil.</div>}
      <div className="row row-end">
        <button className="btn btn-primary" disabled={busy || !current || !next || mismatch || next !== again}
          onClick={async () => { if (await run(() => api.changePassword(current, next), "Parolanız değiştirildi.")) { setCurrent(""); setNext(""); setAgain(""); } }}>
          Parolayı Değiştir
        </button>
      </div>
    </div>
  );
}

function SystemTab() {
  const { config, info, isAdmin } = useSession();
  const { confirm } = useFeedback();
  const [machine, setMachine] = useState("");
  const backups = useLoad(() => (isAdmin ? api.backups() : Promise.resolve([])));
  const { run, busy } = useAction();
  useEffect(() => { void shell.machineName().then(setMachine); }, []);
  const isMain = config.mode === "main";

  return (
    <div className="stack">
      <div className="card">
        <div className="card-title" style={{ marginBottom: 12 }}>Bağlantı</div>
        {isMain ? (
          <Alert kind="info">
            Bu bilgisayar <b>ana bilgisayar</b>. Bütün kayıtlar burada saklanır. Diğer bilgisayarlar Salonera'yı ilk açtıklarında
            “Diğer bilgisayar”ı seçince bu bilgisayarı (<b>{machine}</b>) kendiliğinden bulur. Bulamazlarsa bu bilgisayarın ağ adresini
            (Windows'ta Ayarlar → Ağ ve İnternet → Özellikler → IPv4 adresi) yazabilirler.
          </Alert>
        ) : (
          <Alert kind="info">Bu bilgisayar ana bilgisayara bağlı: <b>{config.serverUrl}</b> ({info.businessName})</Alert>
        )}
        <div className="muted small" style={{ marginTop: 12 }}>Salonera sürüm {info.version}</div>
      </div>
      {isAdmin && (
        <div className="card">
          <div className="card-head">
            <div>
              <div className="card-title">Yedekler</div>
              <div className="card-sub">Ana bilgisayar her gün otomatik yedek alır; son 60 yedek saklanır.</div>
            </div>
            <div className="row">
              {isMain && <button className="btn" onClick={() => void shell.openDataFolder()}><Icon name="file" /> Klasörü aç</button>}
              <button className="btn btn-primary" disabled={busy} onClick={async () => {
                if (await run(() => api.createBackup(), "Yedek alındı.")) void backups.reload();
              }}><Icon name="download" /> Şimdi yedek al</button>
            </div>
          </div>
          <Alert kind="warning">
            Bilgisayar bozulursa veriler kaybolmasın diye yedek klasörünü ara sıra bir USB belleğe kopyalayın.
          </Alert>
          <div style={{ marginTop: 14 }}>
            {(backups.data ?? []).length === 0 ? <Empty icon="file">Henüz yedek yok.</Empty> : (
              <table className="table">
                <thead><tr><th>Yedek</th><th>Tarih</th><th className="num">Boyut</th></tr></thead>
                <tbody>{backups.data!.slice(0, 15).map((b) => (
                  <tr key={b.fileName}><td>{b.fileName}</td><td>{b.createdAt ? dateTime(b.createdAt) : "—"}</td><td className="num">{(b.sizeBytes / 1024).toFixed(0)} KB</td></tr>
                ))}</tbody>
              </table>
            )}
          </div>
        </div>
      )}
      {!isMain && (
        <div className="card">
          <div className="card-title" style={{ marginBottom: 8 }}>Başka bir ana bilgisayara bağlan</div>
          <p className="muted" style={{ marginBottom: 14 }}>Ana bilgisayar değiştiyse bağlantıyı sıfırlayın; Salonera yeniden başlarken ağda arama yapar.</p>
          <button className="btn btn-danger" onClick={async () => {
            if (await confirm({ title: "Bağlantı sıfırlansın mı?", message: "Oturumunuz kapanacak ve ana bilgisayar yeniden aranacak.", confirmText: "Sıfırla", danger: true })) {
              await shell.reset();
              window.location.reload();
            }
          }}>Bağlantıyı sıfırla</button>
        </div>
      )}
    </div>
  );
}
