import { useEffect, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { Alert, LoadError, Loading, useAction, useFeedback, useLoad } from "../components/ui";
import { dateTime } from "../format";
import type { CalendarAccount, CalendarProvider } from "../types";

const LABEL: Record<CalendarProvider, string> = { google: "Google Takvim", microsoft: "Outlook" };
const ACCOUNT: Record<CalendarProvider, string> = { google: "Google hesabı", microsoft: "Microsoft hesabı (Outlook, Hotmail, Microsoft 365)" };

/** Ayarlar → Takvim eşitleme: Google Takvim / Outlook hesabı bağlama ve eşitleme durumu. */
export function CalendarSyncTab() {
  const { config } = useSession();
  const { confirm, toast } = useFeedback();
  const { run, busy } = useAction();
  const status = useLoad(() => api.calendarSync());
  const s = status.data;
  const waiting = !!s?.pending && !s.pending.error;
  const [copied, setCopied] = useState(false);

  // Bağlanma beklenirken ya da eşitleme sürerken sık, diğer zamanlarda seyrek yenile.
  useEffect(() => {
    const every = waiting || s?.syncing ? 2000 : 15000;
    const timer = window.setInterval(() => void status.reload(), every);
    return () => window.clearInterval(timer);
  }, [waiting, s?.syncing, status.reload]);

  const connect = (provider: CalendarProvider) => {
    if (s?.demo) {
      toast(`Demo: gerçek programda bu düğme ana bilgisayarın tarayıcısında ${LABEL[provider]} izin ekranını açar.`);
      return;
    }
    return run(async () => {
      setCopied(false);
      await api.connectCalendar(provider);
      await status.reload();
    });
  };

  const disconnect = async (a: CalendarAccount) => {
    const ok = await confirm({
      title: "Takvim bağlantısı kaldırılsın mı?",
      message: `${a.email} hesabındaki "${a.calendarName}" takvimi, içindeki Salonera etkinlikleriyle birlikte silinir. Hesaptaki diğer takvimlere dokunulmaz. Salonera'daki kayıtlar olduğu gibi kalır.`,
      confirmText: "Bağlantıyı kaldır",
      danger: true,
    });
    if (ok && (await run(() => api.disconnectCalendar(a.id), "Takvim bağlantısı kaldırıldı."))) void status.reload();
  };

  const syncNow = async () => {
    if (await run(() => api.syncCalendarNow())) {
      toast("Eşitleme başladı; birkaç saniye içinde takvime yansır.");
      window.setTimeout(() => void status.reload(), 2500);
    }
  };

  if (!s) return status.error ? <LoadError message={status.error} onRetry={status.reload} /> : <Loading />;
  const none = !s.googleAvailable && !s.microsoftAvailable;
  const connectedProviders = new Set(s.accounts.map((a) => a.provider));

  return (
    <div className="stack">
      <div className="card stack">
        <div className="card-head" style={{ marginBottom: 0 }}>
          <div>
            <div className="card-title">Google Takvim ve Outlook</div>
            <div className="card-sub">
              Salonera'daki organizasyonlar bağladığınız takvimde de görünür; telefondan, tabletten bakabilirsiniz.
              Bir kayıt eklenince, değişince ya da iptal edilince takvim birkaç saniye içinde güncellenir.
            </div>
          </div>
        </div>

        {none && (
          <Alert kind="warning">
            Bu Salonera sürümünde takvim bağlantısı henüz ayarlanmamış (Google / Microsoft uygulama kaydı gerekli).
            Salonera'yı size kuran kişiye haber verin.
          </Alert>
        )}

        {s.demo && (
          <Alert kind="info">
            <b>Demo ortamı:</b> düğmeler gerçek programdaki gibi görünür ama burada gerçek bir Google / Outlook hesabı bağlanmaz.
          </Alert>
        )}

        {s.pending && !s.pending.error && (
          <Alert kind="info">
            <div className="stack" style={{ gap: 8 }}>
              <div>
                <b>Ana bilgisayarda tarayıcı açıldı.</b> {ACCOUNT[s.pending.provider]} ile giriş yapıp Salonera'ya takvim izni verin.
                İzin verince bu ekran kendiliğinden güncellenir.
                {config.mode !== "main" && <> İzin, Salonera'nın kurulu olduğu <b>ana bilgisayarın</b> tarayıcısında verilmelidir.</>}
              </div>
              <div className="row" style={{ gap: 8 }}>
                <span className="muted small">Tarayıcı açılmadıysa bağlantıyı kopyalayıp ana bilgisayardaki tarayıcıya yapıştırın.</span>
                <button className="btn btn-sm" onClick={async () => {
                  try { await navigator.clipboard.writeText(s.pending!.authUrl); setCopied(true); } catch { toast("Kopyalanamadı.", true); }
                }}><Icon name="file" size={15} /> {copied ? "Kopyalandı" : "Bağlantıyı kopyala"}</button>
                <button className="btn btn-sm" onClick={() => void run(async () => { await api.cancelCalendarConnect(); await status.reload(); })}>Vazgeç</button>
              </div>
            </div>
          </Alert>
        )}
        {s.pending?.error && (
          <Alert kind="danger">
            <div className="row" style={{ justifyContent: "space-between", gap: 10 }}>
              <span><b>{LABEL[s.pending.provider]} bağlanamadı:</b> {s.pending.error}</span>
              <button className="btn btn-sm" onClick={() => void run(async () => { await api.cancelCalendarConnect(); await status.reload(); })}>Tamam</button>
            </div>
          </Alert>
        )}

        {s.accounts.map((a) => (
          <div key={a.id} className="cal-account">
            <div className="row" style={{ gap: 14, alignItems: "flex-start", flexWrap: "wrap" }}>
              <span className="qi">
                <Icon name="calendar" size={21} />
              </span>
              <div style={{ flex: 1, minWidth: 220 }}>
                <div className="strong" style={{ fontSize: 17 }}>{LABEL[a.provider]} · {a.email}</div>
                <div className="muted small">
                  “{a.calendarName}” takvimi · {a.eventCount} organizasyon takvimde
                  {a.lastSyncAt ? <> · son eşitleme {dateTime(a.lastSyncAt)}</> : <> · ilk eşitleme bekleniyor</>}
                  {s.syncing && <> · <b>eşitleniyor…</b></>}
                </div>
              </div>
              <div className="row" style={{ gap: 8 }}>
                {a.needsReconnect ? (
                  <button className="btn btn-primary btn-sm" disabled={busy || none} onClick={() => void connect(a.provider)}><Icon name="refresh" size={15} /> Yeniden bağla</button>
                ) : (
                  <button className="btn btn-sm" disabled={busy || s.syncing} onClick={() => void syncNow()}><Icon name="refresh" size={15} /> Şimdi eşitle</button>
                )}
                <button className="btn btn-sm btn-danger" disabled={busy} onClick={() => void disconnect(a)}><Icon name="trash" size={15} /> Kaldır</button>
              </div>
            </div>
            {a.lastError && <div style={{ marginTop: 12 }}><Alert kind={a.needsReconnect ? "danger" : "warning"}>{a.lastError}</Alert></div>}
          </div>
        ))}

        {!none && (
          <div className="row" style={{ gap: 10, flexWrap: "wrap" }}>
            {(["google", "microsoft"] as const).map((p) => {
              const available = p === "google" ? s.googleAvailable : s.microsoftAvailable;
              if (!available) return null;
              return (
                <button key={p} className={connectedProviders.has(p) ? "btn" : "btn btn-primary"} disabled={busy || waiting} onClick={() => void connect(p)}>
                  <Icon name="plus" /> {connectedProviders.has(p) ? `Başka bir ${p === "google" ? "Google" : "Microsoft"} hesabı bağla` : `${LABEL[p]} bağla`}
                </button>
              );
            })}
          </div>
        )}
      </div>

      <div className="card stack" style={{ gap: 10 }}>
        <div className="card-title">Takvime ne gider?</div>
        <ul className="muted" style={{ margin: 0, paddingLeft: 20, lineHeight: 1.7 }}>
          <li>Kesin kayıtlar ve ön rezervasyonlar. Ön rezervasyonların başında <b>“(Opsiyon)”</b> yazar; süresi dolunca takvimden silinir.</li>
          <li>İptal edilen kayıt takvimden silinir. Tarih, saat, salon, davetli değişince takvimdeki etkinlik de değişir.</li>
          <li>Etkinlikte: müşteri adı ve telefonu, gelin–damat, salon, saat, davetli sayısı, paket, menü ve ek hizmetler.
            <b> Fiyat, ödeme ve borç bilgisi gönderilmez.</b></li>
          <li>Etkinlikler hesabınızda Salonera'nın açtığı ayrı bir takvime yazılır; diğer takvimlerinize dokunulmaz.
            Telefonda görmek için takvim uygulamasında bu takvimin işaretli olması yeterli.</li>
          <li>Eşitleme tek yönlüdür: değişiklikleri Salonera'da yapın. Takvimde yapılan değişiklik Salonera'ya geçmez.</li>
          <li>Eşitlemeyi ana bilgisayar yapar; ana bilgisayar kapalıyken ya da internet yokken yapılan değişiklikler
            bağlantı gelince takvime yazılır.</li>
        </ul>
      </div>
    </div>
  );
}
