//! Takvim eşitleme: rezervasyonlar Google Takvim'e ve/veya Outlook'a (Outlook.com, Microsoft 365) tek yönlü aktarılır.
//!
//! - Salonera kaynaktır. Takvimde yapılan değişiklik Salonera'ya geçmez.
//! - Her bağlı hesapta Salonera'nın kendi açtığı ayrı bir takvim ("Salonera – <işletme>") kullanılır;
//!   kişinin diğer takvimlerine dokunulmaz.
//! - Kesin kayıtlar ve süresi dolmamış ön rezervasyonlar ("(Opsiyon)") takvimde görünür. İptal edilen,
//!   süresi dolan ön rezervasyon takvimden silinir.
//! - Eşitleme "olması gereken" ile "takvimde olan"ı karşılaştırır: herhangi bir değişiklikten ~1,5 sn sonra
//!   ve ayrıca 5 dakikada bir çalışır. İnternet yoksa sonraki denemede kaldığı yerden devam eder.
//! - Ana bilgisayarda çalışır (giriş anahtarları ve eşleşmeler ana veritabanında durur).

mod event;
mod oauth;
mod remote;

use std::collections::{HashMap, HashSet};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex, RwLock};
use std::time::Duration;

use chrono::NaiveDate;
use rusqlite::{params, Connection, OptionalExtension};
use serde::{Deserialize, Serialize};
use tokio::sync::Notify;
use tokio::task::AbortHandle;

pub use event::{render, CalendarEvent, EventSource};
use remote::{Remote, RemoteError};

use crate::error::{AppError, AppResult};
use crate::model::{EventKind, Status};
use crate::util::{fmt_date, fmt_datetime, parse_date, parse_datetime};
use crate::{App, Ctx};

/// Bugünden bu kadar gün öncesine kadar biten organizasyonlar da ilk bağlanışta takvime yazılır.
const PAST_DAYS: i64 = 60;
const PERIODIC: Duration = Duration::from_secs(5 * 60);
const DEBOUNCE: Duration = Duration::from_millis(1500);

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum Provider {
    Google,
    Microsoft,
}

impl Provider {
    pub fn as_str(self) -> &'static str {
        match self {
            Provider::Google => "google",
            Provider::Microsoft => "microsoft",
        }
    }

    pub fn parse(value: &str) -> Option<Self> {
        match value {
            "google" => Some(Provider::Google),
            "microsoft" => Some(Provider::Microsoft),
            _ => None,
        }
    }

    pub fn label(self) -> &'static str {
        match self {
            Provider::Google => "Google Takvim",
            Provider::Microsoft => "Outlook",
        }
    }
}

/// Bir takvim hizmetinin uygulama kaydı (Google Cloud / Microsoft Entra) ve adresleri.
#[derive(Clone, Debug)]
pub struct ProviderConfig {
    pub client_id: String,
    /// Google masaüstü uygulamalarında da istenir (gizli sayılmaz). Microsoft'ta yoktur.
    pub client_secret: Option<String>,
    pub auth_url: String,
    pub token_url: String,
    pub api_base: String,
}

#[derive(Clone, Debug, Default)]
pub struct SyncConfig {
    pub google: Option<ProviderConfig>,
    pub microsoft: Option<ProviderConfig>,
    /// Hesap bağlarken ana bilgisayarın tarayıcısı açılsın mı (testlerde kapalı).
    pub open_browser: bool,
    /// Demo ortamı: düğmeler görünür ama gerçek hesap bağlanmaz.
    pub demo: bool,
}

impl SyncConfig {
    /// Uygulama kimlikleri derleme sırasında (`SALONERA_GOOGLE_CLIENT_ID` vb.) verilir; aynı adlı
    /// ortam değişkeni varsa o kullanılır.
    pub fn from_env() -> SyncConfig {
        fn var(name: &str, built: Option<&'static str>) -> Option<String> {
            std::env::var(name)
                .ok()
                .or(built.map(String::from))
                .map(|v| v.trim().to_string())
                .filter(|v| !v.is_empty())
        }
        let google = var("SALONERA_GOOGLE_CLIENT_ID", option_env!("SALONERA_GOOGLE_CLIENT_ID")).map(|client_id| ProviderConfig {
            client_id,
            client_secret: var("SALONERA_GOOGLE_CLIENT_SECRET", option_env!("SALONERA_GOOGLE_CLIENT_SECRET")),
            auth_url: "https://accounts.google.com/o/oauth2/v2/auth".into(),
            token_url: "https://oauth2.googleapis.com/token".into(),
            api_base: "https://www.googleapis.com".into(),
        });
        let microsoft =
            var("SALONERA_MICROSOFT_CLIENT_ID", option_env!("SALONERA_MICROSOFT_CLIENT_ID")).map(|client_id| ProviderConfig {
                client_id,
                client_secret: None,
                auth_url: "https://login.microsoftonline.com/common/oauth2/v2.0/authorize".into(),
                token_url: "https://login.microsoftonline.com/common/oauth2/v2.0/token".into(),
                api_base: "https://graph.microsoft.com/v1.0".into(),
            });
        SyncConfig { google, microsoft, open_browser: true, demo: false }
    }

    fn get(&self, provider: Provider) -> Option<&ProviderConfig> {
        match provider {
            Provider::Google => self.google.as_ref(),
            Provider::Microsoft => self.microsoft.as_ref(),
        }
    }
}

struct Pending {
    id: String,
    provider: Provider,
    auth_url: String,
    error: Option<String>,
    abort: Option<AbortHandle>,
}

/// Uygulama içinde tek bir tane bulunur (`App::calsync`).
pub struct CalSync {
    config: RwLock<SyncConfig>,
    wake: Notify,
    http: reqwest::Client,
    pending: Mutex<Option<Pending>>,
    running: tokio::sync::Mutex<()>,
    syncing: AtomicBool,
}

impl CalSync {
    pub fn new(config: SyncConfig) -> CalSync {
        CalSync {
            config: RwLock::new(config),
            wake: Notify::new(),
            http: reqwest::Client::builder()
                .timeout(Duration::from_secs(30))
                .user_agent("Salonera")
                .build()
                .unwrap_or_default(),
            pending: Mutex::new(None),
            running: tokio::sync::Mutex::new(()),
            syncing: AtomicBool::new(false),
        }
    }

    /// Testler sahte takvim sunucusunu bu yolla verir.
    pub fn configure(&self, config: SyncConfig) {
        *self.config.write().unwrap() = config;
    }

    fn provider_config(&self, provider: Provider) -> Option<ProviderConfig> {
        self.config.read().unwrap().get(provider).cloned()
    }

    fn opens_browser(&self) -> bool {
        self.config.read().unwrap().open_browser
    }

    fn http(&self) -> &reqwest::Client {
        &self.http
    }

    /// Bir kayıt değişti: eşitleme kısa süre sonra çalışır.
    pub fn changed(&self) {
        self.wake.notify_one();
    }

    fn set_pending(&self, pending: Pending) {
        let mut slot = self.pending.lock().unwrap();
        if let Some(old) = slot.take() {
            if let Some(abort) = old.abort {
                abort.abort();
            }
        }
        *slot = Some(pending);
    }

    fn finish_pending(&self, id: &str, error: Option<String>) {
        let mut slot = self.pending.lock().unwrap();
        if slot.as_ref().is_some_and(|p| p.id == id) {
            match error {
                Some(message) => {
                    let p = slot.as_mut().unwrap();
                    p.error = Some(message);
                    p.abort = None;
                }
                None => *slot = None,
            }
        }
    }

    pub fn cancel_pending(&self) {
        if let Some(old) = self.pending.lock().unwrap().take() {
            if let Some(abort) = old.abort {
                abort.abort();
            }
        }
    }
}

pub(crate) fn unix_now() -> i64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or_default()
}

// ---------------- Ekrana giden durum ----------------

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CalendarAccountDto {
    pub id: String,
    pub provider: Provider,
    pub email: String,
    pub calendar_name: String,
    pub connected_at: String,
    pub last_sync_at: Option<String>,
    pub last_error: Option<String>,
    pub needs_reconnect: bool,
    pub event_count: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PendingDto {
    pub provider: Provider,
    pub auth_url: String,
    pub error: Option<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CalendarSyncStatus {
    pub google_available: bool,
    pub microsoft_available: bool,
    pub accounts: Vec<CalendarAccountDto>,
    pub pending: Option<PendingDto>,
    pub syncing: bool,
    pub demo: bool,
}

pub fn status(app: &App, ctx: &Ctx) -> AppResult<CalendarSyncStatus> {
    ctx.user.require_admin()?;
    let accounts = app.with_db(|c| {
        let mut s = c.prepare(
            "SELECT a.id, a.provider, a.email, a.calendar_name, a.connected_at, a.last_sync_at, a.last_error, a.needs_reconnect,
                    (SELECT COUNT(*) FROM calendar_events e WHERE e.account_id = a.id)
             FROM calendar_accounts a ORDER BY a.connected_at",
        )?;
        let rows = s
            .query_map([], |r| {
                Ok(CalendarAccountDto {
                    id: r.get(0)?,
                    provider: Provider::parse(&r.get::<_, String>(1)?).unwrap_or(Provider::Google),
                    email: r.get(2)?,
                    calendar_name: r.get(3)?,
                    connected_at: r.get(4)?,
                    last_sync_at: r.get(5)?,
                    last_error: r.get(6)?,
                    needs_reconnect: r.get(7)?,
                    event_count: r.get(8)?,
                })
            })?
            .collect::<Result<Vec<_>, _>>()?;
        Ok(rows)
    })?;
    let config = app.calsync.config.read().unwrap().clone();
    let pending = app.calsync.pending.lock().unwrap().as_ref().map(|p| PendingDto {
        provider: p.provider,
        auth_url: p.auth_url.clone(),
        error: p.error.clone(),
    });
    Ok(CalendarSyncStatus {
        google_available: config.google.is_some() || config.demo,
        microsoft_available: config.microsoft.is_some() || config.demo,
        accounts,
        pending,
        syncing: app.calsync.syncing.load(Ordering::Relaxed),
        demo: config.demo,
    })
}

/// Hesap bağlamayı başlatır; tarayıcıda açılacak izin adresini döndürür.
pub async fn connect(app: Arc<App>, ctx: &Ctx, provider: Provider) -> AppResult<String> {
    ctx.user.require_admin()?;
    if app.calsync.config.read().unwrap().demo {
        return Err(AppError::Rule(format!(
            "Bu bir demo: gerçek {} hesabı bağlanmaz. Gerçek programda bu düğme ana bilgisayarın tarayıcısında hesap iznini açar.",
            provider.label()
        )));
    }
    oauth::start(app, provider, ctx.user.id.clone()).await
}

/// Bağlantıyı kaldırır. Salonera'nın açtığı takvim de (içindeki etkinliklerle) silinir.
pub async fn disconnect(app: &App, ctx: &Ctx, id: &str) -> AppResult<()> {
    ctx.user.require_admin()?;
    let account = app.with_db(|c| load_account(c, id))?;
    if let Some(cfg) = app.calsync.provider_config(account.provider) {
        if let Ok(token) = access_token(app, &account, &cfg).await {
            let api = Remote { http: &app.calsync.http, provider: account.provider, cfg: &cfg, token };
            if let Err(error) = api.delete_calendar(&account.calendar_id).await {
                tracing::warn!("takvim silinemedi: {error}");
            }
        }
    }
    let now = fmt_datetime(ctx.now);
    app.with_db(|c| {
        c.execute("DELETE FROM calendar_accounts WHERE id = ?1", [id])?;
        c.execute(
            "INSERT INTO audit (at, user_id, action, detail) VALUES (?1, ?2, 'takvim_kaldirildi', ?3)",
            params![now, ctx.user.id, format!("{} {}", account.provider.label(), account.email)],
        )?;
        Ok(())
    })
}

// ---------------- Veritabanı ----------------

struct Account {
    id: String,
    provider: Provider,
    email: String,
    refresh_token: String,
    access_token: Option<String>,
    access_expires: i64,
    calendar_id: String,
}

fn load_account(conn: &Connection, id: &str) -> AppResult<Account> {
    conn.query_row(
        "SELECT id, provider, email, refresh_token, access_token, access_expires, calendar_id FROM calendar_accounts WHERE id = ?1",
        [id],
        |r| {
            Ok(Account {
                id: r.get(0)?,
                provider: Provider::parse(&r.get::<_, String>(1)?).unwrap_or(Provider::Google),
                email: r.get(2)?,
                refresh_token: r.get(3)?,
                access_token: r.get(4)?,
                access_expires: r.get::<_, Option<i64>>(5)?.unwrap_or(0),
                calendar_id: r.get(6)?,
            })
        },
    )
    .optional()?
    .ok_or_else(|| AppError::NotFound("Takvim bağlantısı bulunamadı.".into()))
}

struct Mapping {
    event_id: String,
    hash: String,
}

fn mappings(conn: &Connection, account_id: &str) -> AppResult<HashMap<String, Mapping>> {
    let mut s = conn.prepare("SELECT reservation_id, event_id, hash FROM calendar_events WHERE account_id = ?1")?;
    let rows = s
        .query_map([account_id], |r| Ok((r.get::<_, String>(0)?, Mapping { event_id: r.get(1)?, hash: r.get(2)? })))?
        .collect::<Result<HashMap<_, _>, _>>()?;
    Ok(rows)
}

/// Takvimde olması gereken etkinlikler: iptal edilmemiş, süresi dolmamış kayıtlar. Bugünden `PAST_DAYS`
/// önce bitmiş olanlar yalnızca takvimde zaten varsa tutulur (yeni bağlanan hesaba eski yıllar yığılmasın).
pub fn desired(conn: &Connection, today: NaiveDate, keep: &HashSet<String>) -> AppResult<Vec<(String, CalendarEvent)>> {
    let (business, address): (String, Option<String>) = conn
        .query_row("SELECT business_name, address FROM settings WHERE id = 1", [], |r| Ok((r.get(0)?, r.get(1)?)))
        .optional()?
        .unwrap_or_default();
    let hall_order: HashMap<String, usize> = {
        let mut s = conn.prepare("SELECT id FROM halls ORDER BY sort, name")?;
        let ids = s.query_map([], |r| r.get::<_, String>(0))?.collect::<Result<Vec<_>, _>>()?;
        ids.into_iter().enumerate().map(|(i, id)| (id, i)).collect()
    };
    let mut extras: HashMap<String, Vec<String>> = HashMap::new();
    {
        let mut s = conn.prepare(
            "SELECT x.reservation_id, x.name, x.group_name FROM reservation_extras x
             JOIN reservations r ON r.id = x.reservation_id WHERE r.status <> 'iptal' ORDER BY x.rowid",
        )?;
        let rows = s.query_map([], |r| Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?, r.get::<_, Option<String>>(2)?)))?;
        for row in rows {
            let (id, name, group) = row?;
            let label = match group.filter(|g| !g.trim().is_empty()) {
                Some(g) => format!("{g}: {name}"),
                None => name,
            };
            extras.entry(id).or_default().push(label);
        }
    }

    let window = fmt_datetime((today - chrono::Duration::days(PAST_DAYS)).and_hms_opt(0, 0, 0).unwrap_or_default());
    let mut s = conn.prepare(
        "SELECT r.id, r.no, r.starts_at, r.ends_at, r.kind, r.guest_count, r.status, r.option_until, r.package_name, r.menu_name,
                h.id, h.name, c.full_name, c.phone, c.phone2, c.bride_name, c.groom_name
         FROM reservations r JOIN halls h ON h.id = r.hall_id JOIN customers c ON c.id = r.customer_id
         WHERE r.status <> 'iptal' AND NOT (r.status = 'on_rezervasyon' AND r.option_until < ?1)
         ORDER BY r.starts_at",
    )?;
    let rows = s.query_map([fmt_date(today)], |r| {
        Ok((
            r.get::<_, String>(0)?,
            r.get::<_, String>(3)?,
            EventSource {
                no: r.get(1)?,
                start: parse_datetime(&r.get::<_, String>(2)?),
                end: parse_datetime(&r.get::<_, String>(3)?),
                kind: r.get::<_, EventKind>(4)?,
                guest_count: r.get(5)?,
                status: r.get::<_, Status>(6)?,
                option_until: r.get::<_, Option<String>>(7)?.map(|v| parse_date(&v)),
                package_name: r.get(8)?,
                menu_name: r.get(9)?,
                hall_index: hall_order.get(&r.get::<_, String>(10)?).copied().unwrap_or(0),
                hall_name: r.get(11)?,
                customer_name: r.get(12)?,
                phone: r.get(13)?,
                phone2: r.get(14)?,
                bride_name: r.get(15)?,
                groom_name: r.get(16)?,
                extras: Vec::new(),
            },
        ))
    })?;
    let mut list = Vec::new();
    for row in rows {
        let (id, ends, mut src) = row?;
        if ends < window && !keep.contains(&id) {
            continue;
        }
        src.extras = extras.remove(&id).unwrap_or_default();
        list.push((id.clone(), render(&src, &business, address.as_deref())));
    }
    Ok(list)
}

#[derive(Debug, PartialEq)]
pub enum Op {
    Create { reservation_id: String, event: CalendarEvent },
    Update { reservation_id: String, event_id: String, event: CalendarEvent },
    Delete { reservation_id: String, event_id: String },
}

fn plan(desired: Vec<(String, CalendarEvent)>, mut mapped: HashMap<String, Mapping>) -> Vec<Op> {
    let mut ops = Vec::new();
    for (reservation_id, event) in desired {
        match mapped.remove(&reservation_id) {
            None => ops.push(Op::Create { reservation_id, event }),
            Some(m) if m.hash != event.hash() => ops.push(Op::Update { reservation_id, event_id: m.event_id, event }),
            Some(_) => {}
        }
    }
    let mut gone: Vec<_> = mapped.into_iter().collect();
    gone.sort_by(|a, b| a.0.cmp(&b.0));
    ops.extend(gone.into_iter().map(|(reservation_id, m)| Op::Delete { reservation_id, event_id: m.event_id }));
    ops
}

// ---------------- Eşitleme ----------------

/// Ana bilgisayarda sürekli çalışır: değişiklik olunca ve 5 dakikada bir eşitler.
pub async fn worker(app: Arc<App>) {
    loop {
        tokio::select! {
            _ = app.calsync.wake.notified() => {}
            _ = tokio::time::sleep(PERIODIC) => {}
        }
        tokio::time::sleep(DEBOUNCE).await;
        sync_all(&app).await;
    }
}

/// Bütün bağlı hesapları hemen eşitler (bitene kadar bekler).
pub async fn sync_all(app: &App) {
    let _guard = app.calsync.running.lock().await;
    let ids: Vec<String> = app
        .with_db(|c| {
            let mut s = c.prepare("SELECT id FROM calendar_accounts WHERE needs_reconnect = 0 ORDER BY connected_at")?;
            let ids = s.query_map([], |r| r.get(0))?.collect::<Result<Vec<_>, _>>()?;
            Ok(ids)
        })
        .unwrap_or_default();
    if ids.is_empty() {
        return;
    }
    app.calsync.syncing.store(true, Ordering::Relaxed);
    for id in ids {
        let outcome = sync_account(app, &id).await;
        let now = fmt_datetime(app.now());
        let _ = app.with_db(|c| {
            match &outcome {
                Ok(None) => c.execute(
                    "UPDATE calendar_accounts SET last_sync_at = ?1, last_error = NULL WHERE id = ?2",
                    params![now, id],
                )?,
                Ok(Some(warning)) => c.execute(
                    "UPDATE calendar_accounts SET last_sync_at = ?1, last_error = ?2 WHERE id = ?3",
                    params![now, warning, id],
                )?,
                Err(SyncError::Reconnect(message)) => c.execute(
                    "UPDATE calendar_accounts SET needs_reconnect = 1, access_token = NULL, last_error = ?1 WHERE id = ?2",
                    params![message, id],
                )?,
                Err(SyncError::Failed(message)) => {
                    c.execute("UPDATE calendar_accounts SET last_error = ?1 WHERE id = ?2", params![message, id])?
                }
            };
            Ok(())
        });
    }
    app.calsync.syncing.store(false, Ordering::Relaxed);
}

enum SyncError {
    Reconnect(String),
    Failed(String),
}

impl From<AppError> for SyncError {
    fn from(error: AppError) -> Self {
        SyncError::Failed(error.to_string())
    }
}

fn reconnect_message(provider: Provider) -> String {
    format!("{} izni kaldırılmış veya süresi dolmuş. Ayarlar → Takvim'den hesabı yeniden bağlayın.", provider.label())
}

/// Geçerli erişim anahtarı; süresi dolmak üzereyse yenileyip kaydeder.
async fn access_token(app: &App, account: &Account, cfg: &ProviderConfig) -> Result<String, SyncError> {
    if let Some(token) = &account.access_token {
        if account.access_expires > unix_now() + 60 {
            return Ok(token.clone());
        }
    }
    let tokens = match remote::refresh(&app.calsync.http, account.provider, cfg, &account.refresh_token).await {
        Ok(t) => t,
        Err(RemoteError::Auth(_)) => return Err(SyncError::Reconnect(reconnect_message(account.provider))),
        Err(e) => return Err(SyncError::Failed(e.to_string())),
    };
    let refresh_token = tokens.refresh_token.clone().unwrap_or_else(|| account.refresh_token.clone());
    let expires = unix_now() + tokens.expires_in;
    app.with_db(|c| {
        c.execute(
            "UPDATE calendar_accounts SET access_token = ?1, access_expires = ?2, refresh_token = ?3 WHERE id = ?4",
            params![tokens.access_token, expires, refresh_token, account.id],
        )?;
        Ok(())
    })?;
    Ok(tokens.access_token)
}

/// Bir hesabı eşitler. `Ok(Some(..))`: bazı etkinlikler yazılamadı (sonraki turda yeniden denenir).
async fn sync_account(app: &App, id: &str) -> Result<Option<String>, SyncError> {
    let today = app.now().date();
    let (account, ops) = app.with_db(|c| {
        let account = load_account(c, id)?;
        let mapped = mappings(c, id)?;
        let keep: HashSet<String> = mapped.keys().cloned().collect();
        let wanted = desired(c, today, &keep)?;
        Ok((account, plan(wanted, mapped)))
    })?;
    if ops.is_empty() {
        return Ok(None);
    }
    let cfg = app
        .calsync
        .provider_config(account.provider)
        .ok_or_else(|| SyncError::Failed(format!("Bu Salonera sürümünde {} bağlantısı ayarlanmamış.", account.provider.label())))?;
    let token = access_token(app, &account, &cfg).await?;
    let api = Remote { http: &app.calsync.http, provider: account.provider, cfg: &cfg, token };
    let calendar = account.calendar_id.as_str();

    let save = |reservation_id: &str, event_id: &str, hash: &str| {
        app.with_db(|c| {
            c.execute(
                "INSERT INTO calendar_events (account_id, reservation_id, event_id, hash) VALUES (?1, ?2, ?3, ?4)
                 ON CONFLICT (account_id, reservation_id) DO UPDATE SET event_id = excluded.event_id, hash = excluded.hash",
                params![id, reservation_id, event_id, hash],
            )?;
            Ok(())
        })
    };

    let mut failures = 0usize;
    let mut first_failure: Option<String> = None;
    for op in ops {
        let result = match &op {
            Op::Create { reservation_id, event } => match api.create_event(calendar, reservation_id, event).await {
                Ok(event_id) => save(reservation_id, &event_id, &event.hash()).map_err(|e| RemoteError::Failed(e.to_string())),
                Err(e) => Err(e),
            },
            Op::Update { reservation_id, event_id, event } => {
                match api.update_event(calendar, event_id, reservation_id, event).await {
                    Ok(()) => save(reservation_id, event_id, &event.hash()).map_err(|e| RemoteError::Failed(e.to_string())),
                    // Takvimden elle silinmiş: yeniden ekle.
                    Err(RemoteError::NotFound) => match api.create_event(calendar, reservation_id, event).await {
                        Ok(new_id) => save(reservation_id, &new_id, &event.hash()).map_err(|e| RemoteError::Failed(e.to_string())),
                        Err(e) => Err(e),
                    },
                    Err(e) => Err(e),
                }
            }
            Op::Delete { reservation_id, event_id } => match api.delete_event(calendar, event_id).await {
                Ok(()) => app
                    .with_db(|c| {
                        c.execute(
                            "DELETE FROM calendar_events WHERE account_id = ?1 AND reservation_id = ?2",
                            params![id, reservation_id],
                        )?;
                        Ok(())
                    })
                    .map_err(|e| RemoteError::Failed(e.to_string())),
                Err(e) => Err(e),
            },
        };
        match result {
            Ok(()) => {}
            Err(RemoteError::Auth(_)) => return Err(SyncError::Reconnect(reconnect_message(account.provider))),
            // Etkinlik eklerken "bulunamadı": Salonera takvimi elle silinmiş. Yeniden açıp baştan yaz.
            Err(RemoteError::NotFound) => {
                let name = app
                    .with_db(|c| Ok(c.query_row("SELECT calendar_name FROM calendar_accounts WHERE id = ?1", [id], |r| r.get::<_, String>(0))?))?;
                let new_calendar = api.create_calendar(&name).await.map_err(|e| SyncError::Failed(format!("Takvim yeniden açılamadı: {e}")))?;
                app.with_db(|c| {
                    let tx = c.transaction()?;
                    tx.execute("UPDATE calendar_accounts SET calendar_id = ?1 WHERE id = ?2", params![new_calendar, id])?;
                    tx.execute("DELETE FROM calendar_events WHERE account_id = ?1", [id])?;
                    tx.commit()?;
                    Ok(())
                })?;
                app.calsync.changed();
                return Ok(Some("Salonera takvimi silinmişti; yeniden açıldı ve etkinlikler yeniden yazılıyor.".into()));
            }
            Err(RemoteError::Failed(message)) => {
                failures += 1;
                if first_failure.is_none() {
                    first_failure = Some(message);
                }
            }
        }
    }
    Ok(first_failure.map(|m| format!("{failures} kayıt takvime yazılamadı (sonra yeniden denenecek): {m}")))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ev(title: &str) -> CalendarEvent {
        let t = NaiveDate::from_ymd_opt(2026, 6, 1).unwrap().and_hms_opt(19, 0, 0).unwrap();
        CalendarEvent {
            title: title.into(),
            description: String::new(),
            location: String::new(),
            start: t,
            end: t,
            tentative: false,
            hall_index: 0,
        }
    }

    #[test]
    fn plan_creates_updates_and_deletes_only_what_changed() {
        let same = ev("aynı");
        let mut mapped = HashMap::new();
        mapped.insert("a".to_string(), Mapping { event_id: "ea".into(), hash: same.hash() });
        mapped.insert("b".to_string(), Mapping { event_id: "eb".into(), hash: "eski".into() });
        mapped.insert("c".to_string(), Mapping { event_id: "ec".into(), hash: "x".into() });
        let ops = plan(vec![("a".into(), same), ("b".into(), ev("yeni")), ("d".into(), ev("d"))], mapped);
        assert_eq!(
            ops,
            vec![
                Op::Update { reservation_id: "b".into(), event_id: "eb".into(), event: ev("yeni") },
                Op::Create { reservation_id: "d".into(), event: ev("d") },
                Op::Delete { reservation_id: "c".into(), event_id: "ec".into() },
            ]
        );
    }
}
