import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import { api, configureApi, setToken } from "./api";
import { BrandMark } from "./components/Ornament";
import { Icon, type IconName } from "./components/Icon";
import { FeedbackProvider, initials, Loading, useFeedback } from "./components/ui";
import { roleLabels } from "./format";
import { shell } from "./shell";
import type { ServerInfo, ShellConfig, User } from "./types";
import { FirstRun } from "./pages/FirstRun";
import { Setup } from "./pages/Setup";
import { Login } from "./pages/Login";
import { Dashboard } from "./pages/Dashboard";
import { CalendarPage } from "./pages/CalendarPage";
import { Wizard } from "./pages/Wizard";
import { ReservationPage } from "./pages/ReservationPage";
import { Customers } from "./pages/Customers";
import { CustomerPage } from "./pages/CustomerPage";
import { Payments } from "./pages/Payments";
import { Reports } from "./pages/Reports";
import { SettingsPage } from "./pages/SettingsPage";
import { PrintPage } from "./pages/PrintPage";
import { ProgramPage } from "./pages/ProgramPage";
import { MenuPage } from "./pages/MenuPage";

export type Route =
  | { name: "home" }
  | { name: "calendar"; month?: string }
  | { name: "new"; date?: string; hallId?: string; customerId?: string }
  | { name: "reservation"; id: string }
  | { name: "program"; id: string }
  | { name: "menu"; id: string }
  | { name: "customers" }
  | { name: "customer"; id: string }
  | { name: "payments" }
  | { name: "reports" }
  | { name: "settings"; tab?: string }
  | { name: "print"; kind: "contract" | "receipt" | "program" | "menu"; id: string };

interface Session {
  user: User;
  info: ServerInfo;
  config: ShellConfig;
  isAdmin: boolean;
  navigate: (route: Route) => void;
  /** Mevcut sayfanın yerine açar (geri tuşu bu sayfaya dönmez). */
  replace: (route: Route) => void;
  back: () => void;
  /**
   * Sayfada kaydedilmemiş değişiklik varsa uyarı metni verilir; menüden veya "Geri" ile çıkarken sorulur.
   * Değişiklik kalmayınca null verilir.
   */
  setLeaveGuard: (message: string | null) => void;
  refreshInfo: () => Promise<void>;
  signOut: () => void;
}

const SessionContext = createContext<Session>(null!);
export const useSession = () => useContext(SessionContext);

type Phase =
  | { kind: "loading" }
  | { kind: "first-run" }
  | { kind: "unreachable"; config: ShellConfig; message: string }
  | { kind: "setup"; config: ShellConfig }
  | { kind: "not-set-up"; config: ShellConfig }
  | { kind: "login"; config: ShellConfig; info: ServerInfo }
  | { kind: "ready"; config: ShellConfig; info: ServerInfo; user: User };

export default function App() {
  return (
    <FeedbackProvider>
      <Root />
    </FeedbackProvider>
  );
}

function Root() {
  const [phase, setPhase] = useState<Phase>({ kind: "loading" });

  const start = useCallback(async () => {
    setPhase({ kind: "loading" });
    const config = await shell.getConfig();
    if (!config.mode || !config.serverUrl) {
      setPhase({ kind: "first-run" });
      return;
    }
    configureApi(config.serverUrl, () => {
      setToken(null);
      setPhase((p) => (p.kind === "ready" ? { kind: "login", config: p.config, info: p.info } : p));
    });
    // Ana bilgisayarda sunucu birkaç saniyede açılır; birkaç kez dene.
    let lastError = "";
    for (let attempt = 0; attempt < 6; attempt++) {
      try {
        const info = await api.info();
        if (!info.isSetUp) setPhase(config.mode === "main" ? { kind: "setup", config } : { kind: "not-set-up", config });
        else setPhase({ kind: "login", config, info });
        return;
      } catch (e) {
        lastError = e instanceof Error ? e.message : String(e);
        await new Promise((r) => setTimeout(r, 700));
      }
    }
    setPhase({ kind: "unreachable", config, message: lastError });
  }, []);

  useEffect(() => { void start(); }, [start]);

  switch (phase.kind) {
    case "loading":
      return <Loading text="Salonera açılıyor…" />;
    case "first-run":
      return <FirstRun onDone={start} />;
    case "setup":
      return <Setup onDone={start} />;
    case "not-set-up":
    case "unreachable":
      return (
        <ConnectionProblem
          message={phase.kind === "not-set-up"
            ? "Ana bilgisayarda Salonera kurulumu henüz tamamlanmamış. Önce ana bilgisayarda kurulumu bitirin."
            : phase.message}
          isMain={phase.config.mode === "main"}
          onRetry={start}
          onReset={async () => { await shell.reset(); void start(); }}
        />
      );
    case "login":
      return (
        <Login info={phase.info} isMain={phase.config.mode === "main"}
          onLogin={(token, user) => { setToken(token); setPhase({ kind: "ready", config: phase.config, info: phase.info, user }); }}
          onChangeConnection={async () => { await shell.reset(); void start(); }} />
      );
    case "ready":
      return (
        <Workspace config={phase.config} info={phase.info} user={phase.user}
          onInfo={(info) => setPhase({ ...phase, info })}
          onSignOut={() => { void api.logout().catch(() => undefined); setToken(null); setPhase({ kind: "login", config: phase.config, info: phase.info }); }} />
      );
  }
}

function ConnectionProblem({ message, isMain, onRetry, onReset }: {
  message: string; isMain: boolean; onRetry: () => void; onReset: () => void;
}) {
  return (
    <div style={{ display: "grid", placeItems: "center", height: "100%", padding: 24 }}>
      <div className="card" style={{ maxWidth: 560 }}>
        <div className="modal-icon" style={{ background: "var(--danger-soft)", color: "var(--danger)" }}><Icon name="wifi" size={28} /></div>
        <h2 style={{ fontSize: 24, marginBottom: 10 }}>Bağlantı kurulamadı</h2>
        <p style={{ color: "var(--text-2)", fontSize: 17 }}>{message}</p>
        {!isMain && (
          <p className="muted" style={{ marginTop: 12 }}>
            Ana bilgisayarın açık olduğundan, Salonera'nın orada çalıştığından ve iki bilgisayarın aynı ağa (modeme) bağlı olduğundan emin olun.
          </p>
        )}
        <div className="modal-actions">
          <button className="btn" onClick={onReset}>Bağlantı ayarını değiştir</button>
          <button className="btn btn-primary" onClick={onRetry}><Icon name="refresh" /> Tekrar dene</button>
        </div>
      </div>
    </div>
  );
}

interface NavItem { route: Route["name"]; label: string; icon: IconName; admin?: boolean }
const NAV: NavItem[] = [
  { route: "home", label: "Ana Sayfa", icon: "home" },
  { route: "calendar", label: "Takvim", icon: "calendar" },
  { route: "customers", label: "Müşteriler", icon: "users" },
  { route: "payments", label: "Ödemeler", icon: "wallet" },
  { route: "reports", label: "Raporlar", icon: "chart", admin: true },
  { route: "settings", label: "Ayarlar", icon: "settings" },
];

const TITLES: Record<Route["name"], string> = {
  home: "Ana Sayfa", calendar: "Takvim", new: "Yeni Rezervasyon", reservation: "Rezervasyon", program: "Organizasyon Planı", menu: "Yemek Menüsü", customers: "Müşteriler",
  customer: "Müşteri", payments: "Ödemeler", reports: "Raporlar", settings: "Ayarlar", print: "Yazdır",
};

function Workspace({ config, info, user, onInfo, onSignOut }: {
  config: ShellConfig; info: ServerInfo; user: User; onInfo: (info: ServerInfo) => void; onSignOut: () => void;
}) {
  const [history, setHistory] = useState<Route[]>([{ name: "home" }]);
  const route = history[history.length - 1];
  const { confirm } = useFeedback();
  const isAdmin = user.role === "yonetici";

  const navigate = useCallback((next: Route) => {
    setHistory((list) => {
      const top = list[list.length - 1];
      if (JSON.stringify(top) === JSON.stringify(next)) return list;
      // Ana menüden gidilen sayfalarda geçmiş sıfırlanır; içeriden açılanlar üst üste eklenir.
      const isMenu = next.name === "new" || (NAV.some((n) => n.route === next.name) && !("id" in next));
      if (!isMenu) return [...list, next];
      return next.name === "home" ? [{ name: "home" }] : [{ name: "home" }, next];
    });
  }, []);

  const replace = useCallback((next: Route) => setHistory((list) => [...list.slice(0, -1), next]), []);

  const back = useCallback(() => setHistory((list) => (list.length > 1 ? list.slice(0, -1) : list)), []);

  const refreshInfo = useCallback(async () => onInfo(await api.info()), [onInfo]);

  const leaveGuard = useRef<string | null>(null);
  const setLeaveGuard = useCallback((message: string | null) => { leaveGuard.current = message; }, []);

  const leaveWizard = async (go: () => void) => {
    if (leaveGuard.current) {
      const ok = await confirm({ title: "Sayfadan çıkılsın mı?", message: leaveGuard.current, confirmText: "Evet, çık", danger: true });
      if (!ok) return;
      leaveGuard.current = null;
    } else if (route.name === "new") {
      const ok = await confirm({
        title: "Rezervasyondan çıkılsın mı?",
        message: "Yeni rezervasyonu henüz kaydetmediniz. Çıkarsanız yazdığınız bilgiler silinir.",
        confirmText: "Evet, çık",
        danger: true,
      });
      if (!ok) return;
    }
    go();
  };

  const session: Session = { user, info, config, isAdmin, navigate, replace, back, setLeaveGuard, refreshInfo, signOut: onSignOut };
  // Menüden açılan sayfalarda "Geri" gösterilmez; yalnızca bir sayfanın içinden açılanlarda gösterilir.
  const showBack = history.length > 1 && ["reservation", "customer", "program", "menu"].includes(route.name);

  if (route.name === "print") {
    return (
      <SessionContext.Provider value={session}>
        <PrintPage kind={route.kind} id={route.id} />
      </SessionContext.Provider>
    );
  }

  return (
    <SessionContext.Provider value={session}>
      <div className="app">
        <aside className="sidebar no-print">
          <div className="brand">
            <div className="brand-mark"><BrandMark size={48} /></div>
            <div>
              <div className="brand-name">Salonera</div>
              <div className="brand-sub">{info.businessName}</div>
            </div>
          </div>
          <button className="nav-new" onClick={() => leaveWizard(() => navigate({ name: "new" }))}>
            <Icon name="plus" size={22} strokeWidth={2.4} /> Yeni Rezervasyon
          </button>
          <nav className="nav">
            {NAV.filter((n) => !n.admin || isAdmin).map((item) => (
              <button key={item.route}
                className={`nav-item ${route.name === item.route || (item.route === "customers" && route.name === "customer") ? "active" : ""}`}
                onClick={() => leaveWizard(() => navigate({ name: item.route } as Route))}>
                <Icon name={item.icon} size={21} />{item.label}
              </button>
            ))}
          </nav>
          <div className="sidebar-user">
            <div className="avatar">{initials(user.name)}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div className="name" style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{user.name}</div>
              <div className="role">{roleLabels[user.role]}</div>
            </div>
            <button className="btn btn-ghost icon-btn" title="Çıkış yap" style={{ color: "#e8d9de" }}
              onClick={() => leaveWizard(async () => {
                if (await confirm({ title: "Çıkış yapılsın mı?", message: "Oturumunuz kapatılacak.", confirmText: "Çıkış Yap" })) onSignOut();
              })}>
              <Icon name="logout" />
            </button>
          </div>
        </aside>
        <main className="main">
          <header className="topbar no-print">
            {showBack && (
              <button className="btn" onClick={() => leaveWizard(back)}><Icon name="back" size={18} /> Geri</button>
            )}
            <h1>{TITLES[route.name]}</h1>
          </header>
          <Page route={route} key={JSON.stringify(route)} />
        </main>
      </div>
    </SessionContext.Provider>
  );
}

function Page({ route }: { route: Route }) {
  switch (route.name) {
    case "home": return <Dashboard />;
    case "calendar": return <CalendarPage month={route.month} />;
    case "new": return <Wizard date={route.date} hallId={route.hallId} customerId={route.customerId} />;
    case "reservation": return <ReservationPage id={route.id} />;
    case "program": return <ProgramPage id={route.id} />;
    case "menu": return <MenuPage id={route.id} />;
    case "customers": return <Customers />;
    case "customer": return <CustomerPage id={route.id} />;
    case "payments": return <Payments />;
    case "reports": return <Reports />;
    case "settings": return <SettingsPage tab={route.tab} />;
    case "print": return null;
  }
}
