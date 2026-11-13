import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { ApiError } from "../api";
import { moneyInput, monthShort, parseDate, parseMoney, statusLabel } from "../format";
import type { ReservationSummary } from "../types";
import { Icon, type IconName } from "./Icon";

// ---------------- Veri yükleme ----------------
/** Sayfa verisini yükler; hata olursa mesajı ve "Tekrar dene" imkânını verir. */
export function useLoad<T>(load: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const loadRef = useRef(load);
  loadRef.current = load;
  const version = useRef(0);

  const reload = useCallback(async () => {
    const current = ++version.current;
    setLoading(true);
    setError(null);
    try {
      const result = await loadRef.current();
      if (current === version.current) setData(result);
    } catch (e) {
      if (current === version.current) setError(errorText(e));
    } finally {
      if (current === version.current) setLoading(false);
    }
  }, []);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void reload(); }, deps);
  return { data, error, loading, reload, setData };
}

export function errorText(e: unknown): string {
  if (e instanceof ApiError) return e.message;
  if (typeof e === "string") return e;
  return "Beklenmeyen bir sorun oluştu. Tekrar deneyin.";
}

export function Loading({ text = "Yükleniyor…" }: { text?: string }) {
  return <div className="loading"><div className="spinner" /><span>{text}</span></div>;
}

export function LoadError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="alert alert-danger" style={{ alignItems: "center" }}>
      <Icon name="alert" />
      <span style={{ flex: 1 }}>{message}</span>
      <button className="btn" onClick={onRetry}><Icon name="refresh" size={18} /> Tekrar dene</button>
    </div>
  );
}

export function Empty({ icon = "info", children }: { icon?: IconName; children: ReactNode }) {
  return <div className="empty"><Icon name={icon} size={34} /><div>{children}</div></div>;
}

export function Alert({ kind = "info", children }: { kind?: "info" | "danger" | "warning" | "success"; children: ReactNode }) {
  const icon: IconName = kind === "danger" || kind === "warning" ? "alert" : kind === "success" ? "check" : "info";
  return <div className={`alert alert-${kind}`}><Icon name={icon} /><div>{children}</div></div>;
}

// ---------------- Form alanları ----------------
export function Field({ label, hint, children }: { label: string; hint?: ReactNode; children: ReactNode }) {
  return (
    <div className="field">
      <label>{label}</label>
      {children}
      {hint && <span className="hint">{hint}</span>}
    </div>
  );
}

/**
 * Para girişi. Kullanıcı "50000", "50.000" veya "50.000,50" yazabilir; değer kuruş olarak döner.
 * Kutudan çıkınca rakam Türkçe biçimde yeniden yazılır.
 */
export function MoneyInput({ value, onChange, autoFocus, placeholder = "0" }: {
  value: number; onChange: (kurus: number) => void; autoFocus?: boolean; placeholder?: string;
}) {
  const [text, setText] = useState(moneyInput(value));
  const [focused, setFocused] = useState(false);
  useEffect(() => { if (!focused) setText(moneyInput(value)); }, [value, focused]);
  const invalid = text.trim() !== "" && parseMoney(text) === null;
  return (
    <div className="input-wrap">
      <input className="input money" inputMode="decimal" value={text} placeholder={placeholder} autoFocus={autoFocus}
        style={invalid ? { borderColor: "var(--danger)" } : undefined}
        onFocus={() => setFocused(true)}
        onBlur={() => { setFocused(false); setText(moneyInput(parseMoney(text) ?? 0)); }}
        onChange={(e) => { setText(e.target.value); onChange(parseMoney(e.target.value) ?? 0); }} />
      <span className="suffix">₺</span>
    </div>
  );
}

export function NumberInput({ value, onChange, min = 0 }: { value: number; onChange: (v: number) => void; min?: number }) {
  return (
    <input className="input" inputMode="numeric" value={value === 0 ? "" : String(value)}
      onChange={(e) => {
        const digits = e.target.value.replace(/\D/g, "");
        onChange(Math.max(min, digits ? Number(digits) : 0));
      }} />
  );
}

export function TimeSelect({ value, onChange, from = 8, to = 24, extra = 0 }: {
  value: string; onChange: (v: string) => void; from?: number; to?: number; extra?: number;
}) {
  const options: string[] = [];
  for (let m = from * 60; m < to * 60; m += 30) options.push(time(m));
  for (let m = 0; m <= extra * 60; m += 30) options.push(time(m));
  return (
    <select className="select" value={value} onChange={(e) => onChange(e.target.value)}>
      {options.map((o, i) => <option key={`${o}-${i}`} value={o}>{o}{i >= (to - from) * 2 ? " (ertesi gün)" : ""}</option>)}
    </select>
  );
}

const time = (m: number) => `${String(Math.floor(m / 60) % 24).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}`;

// ---------------- Küçük göstergeler ----------------
export function StatusBadge({ r }: { r: Pick<ReservationSummary, "status" | "optionExpired"> }) {
  const kind = r.status === "kesin" ? "success" : r.status === "iptal" ? "muted" : r.optionExpired ? "danger" : "warning";
  return <span className={`badge badge-${kind}`}>{statusLabel(r.status, r.optionExpired)}</span>;
}

/** Renkli simgeli bilgi kartı (ana sayfa, ödemeler, raporlar). */
export type Tone = "wine" | "gold" | "plum" | "green" | "danger";

export function StatCard({ icon, label, value, tone, sub, valueColor }: {
  icon: IconName; label: string; value: string; tone: Tone; sub?: ReactNode; valueColor?: string;
}) {
  return (
    <div className={`card stat rise tone-${tone}`}>
      <div className="stat-icon"><Icon name={icon} size={24} /></div>
      <div className="stat-label">{label}</div>
      <div className="big-number" style={valueColor ? { color: valueColor } : undefined}>{value}</div>
      {sub && <div className="stat-sub">{sub}</div>}
    </div>
  );
}

export function DateBox({ date }: { date: string }) {
  const d = parseDate(date);
  return (
    <div className="date-box">
      <span className="day">{d.getDate()}</span>
      <span className="mon">{monthShort(d.getMonth() + 1)}</span>
    </div>
  );
}

/** Ada göre sabit bir renk (0–4): müşteri listesinde baş harf yuvarlakları renkli görünsün. */
export function hue(name: string) {
  let h = 0;
  for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return h % 5;
}

export function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]).join("").toLocaleUpperCase("tr-TR");
}

// ---------------- Pencere ----------------
export function Modal({ children, onClose, wide }: { children: ReactNode; onClose: () => void; wide?: boolean }) {
  useEffect(() => {
    const key = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [onClose]);
  return (
    <div className="overlay" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className={`modal ${wide ? "modal-wide" : ""}`} role="dialog" aria-modal="true">{children}</div>
    </div>
  );
}

// ---------------- Onay ve bildirim ----------------
interface ConfirmOptions {
  title: string;
  message: ReactNode;
  confirmText?: string;
  danger?: boolean;
  /** Doluysa kullanıcıdan bir açıklama (ör. iptal nedeni) istenir. */
  reasonLabel?: string;
}

interface Feedback {
  confirm: (options: ConfirmOptions) => Promise<string | false>;
  toast: (message: string, error?: boolean) => void;
}

const FeedbackContext = createContext<Feedback>(null!);
export const useFeedback = () => useContext(FeedbackContext);

export function FeedbackProvider({ children }: { children: ReactNode }) {
  const [dialog, setDialog] = useState<(ConfirmOptions & { resolve: (v: string | false) => void }) | null>(null);
  const [reason, setReason] = useState("");
  const [toasts, setToasts] = useState<{ id: number; text: string; error: boolean }[]>([]);

  const confirm = useCallback((options: ConfirmOptions) => new Promise<string | false>((resolve) => {
    setReason("");
    setDialog({ ...options, resolve });
  }), []);

  const toast = useCallback((text: string, error = false) => {
    const id = Date.now() + Math.random();
    setToasts((list) => [...list, { id, text, error }]);
    setTimeout(() => setToasts((list) => list.filter((t) => t.id !== id)), error ? 7000 : 4000);
  }, []);

  const close = (value: string | false) => { dialog?.resolve(value); setDialog(null); };
  const needsReason = !!dialog?.reasonLabel;

  return (
    <FeedbackContext.Provider value={{ confirm, toast }}>
      {children}
      {dialog && (
        <Modal onClose={() => close(false)}>
          <div className="modal-icon" style={{
            background: dialog.danger ? "var(--danger-soft)" : "var(--primary-soft)",
            color: dialog.danger ? "var(--danger)" : "var(--primary)",
          }}>
            <Icon name={dialog.danger ? "alert" : "info"} size={28} />
          </div>
          <h2>{dialog.title}</h2>
          <div style={{ color: "var(--text-2)", fontSize: 17 }}>{dialog.message}</div>
          {needsReason && (
            <div className="field" style={{ marginTop: 18 }}>
              <label>{dialog.reasonLabel}</label>
              <input className="input" autoFocus value={reason} onChange={(e) => setReason(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter" && reason.trim()) close(reason.trim()); }} />
            </div>
          )}
          <div className="modal-actions">
            <button className="btn" onClick={() => close(false)}>Vazgeç</button>
            <button className={`btn ${dialog.danger ? "btn-danger-solid" : "btn-primary"}`} autoFocus={!needsReason}
              disabled={needsReason && !reason.trim()} onClick={() => close(needsReason ? reason.trim() : "ok")}>
              {dialog.confirmText ?? "Evet"}
            </button>
          </div>
        </Modal>
      )}
      <div className="toasts">
        {toasts.map((t) => (
          <div key={t.id} className={`toast ${t.error ? "error" : ""}`}>
            <Icon name={t.error ? "alert" : "check"} /><span>{t.text}</span>
          </div>
        ))}
      </div>
    </FeedbackContext.Provider>
  );
}

/** Bir işlemi çalıştırır; hata olursa kırmızı bildirim gösterir. Başarılıysa true döner. */
export function useAction() {
  const { toast } = useFeedback();
  const [busy, setBusy] = useState(false);
  const run = useCallback(async (work: () => Promise<unknown>, success?: string) => {
    setBusy(true);
    try {
      await work();
      if (success) toast(success);
      return true;
    } catch (e) {
      toast(errorText(e), true);
      return false;
    } finally {
      setBusy(false);
    }
  }, [toast]);
  return { busy, run };
}
