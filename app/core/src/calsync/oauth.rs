//! Hesap bağlama: ana bilgisayarda tarayıcı açılır, kullanıcı Google / Microsoft'ta izin verir,
//! tarayıcı bu bilgisayardaki geçici bir adrese (127.0.0.1 / localhost) geri döner.
//! Parola Salonera'ya hiç gelmez; yalnızca takvim için verilen izin anahtarı saklanır.

use std::sync::Arc;
use std::time::Duration;

use base64::Engine;
use sha2::{Digest, Sha256};
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};

use super::remote::{self, Remote, RemoteError};
use super::{unix_now, Pending, Provider, ProviderConfig};
use crate::error::{AppError, AppResult};
use crate::util::{fmt_datetime, new_id, new_token};
use crate::App;

/// Kullanıcının izin vermesi için beklenen en uzun süre.
const WAIT: Duration = Duration::from_secs(10 * 60);

pub async fn start(app: Arc<App>, provider: Provider, user_id: String) -> AppResult<String> {
    let cfg = app.calsync.provider_config(provider).ok_or_else(|| {
        AppError::Rule(format!("Bu Salonera sürümünde {} bağlantısı ayarlanmamış.", provider.label()))
    })?;
    let listener = TcpListener::bind(("127.0.0.1", 0)).await.map_err(|e| AppError::Internal(e.to_string()))?;
    let port = listener.local_addr().map_err(|e| AppError::Internal(e.to_string()))?.port();
    // Microsoft "localhost" adresine döner; tarayıcı IPv6 (::1) deneyebilir, onu da dinle.
    let listener6 = if provider == Provider::Microsoft { TcpListener::bind(("::1", port)).await.ok() } else { None };
    let redirect = match provider {
        Provider::Google => format!("http://127.0.0.1:{port}"),
        Provider::Microsoft => format!("http://localhost:{port}"),
    };
    let verifier = new_token();
    let challenge = base64::engine::general_purpose::URL_SAFE_NO_PAD.encode(Sha256::digest(verifier.as_bytes()));
    let state = new_token();

    let mut params = vec![
        ("client_id", cfg.client_id.as_str()),
        ("response_type", "code"),
        ("redirect_uri", redirect.as_str()),
        ("scope", remote::scopes(provider)),
        ("state", state.as_str()),
        ("code_challenge", challenge.as_str()),
        ("code_challenge_method", "S256"),
    ];
    match provider {
        // Her bağlanışta yenileme anahtarı gelsin diye onay ekranı her seferinde gösterilir.
        Provider::Google => params.extend([("access_type", "offline"), ("prompt", "consent")]),
        Provider::Microsoft => params.push(("prompt", "select_account")),
    }
    let auth_url = reqwest::Url::parse_with_params(&cfg.auth_url, &params)
        .map_err(|e| AppError::Internal(e.to_string()))?
        .to_string();

    let id = new_id();
    let task_app = app.clone();
    let task_id = id.clone();
    let task = tokio::spawn(async move {
        let result = tokio::time::timeout(
            WAIT,
            wait_for_callback(&task_app, provider, &cfg, listener, listener6, &state, &verifier, &redirect, &user_id),
        )
        .await
        .unwrap_or_else(|_| Err("Süre doldu; izin verilmedi. Tekrar \"Bağla\"ya basın.".into()));
        task_app.calsync.finish_pending(&task_id, result.err());
    });
    app.calsync.set_pending(Pending {
        id,
        provider,
        auth_url: auth_url.clone(),
        error: None,
        abort: Some(task.abort_handle()),
    });
    if app.calsync.opens_browser() {
        open_browser(&auth_url);
    }
    Ok(auth_url)
}

/// Bu bilgisayarın varsayılan tarayıcısında adresi açar.
fn open_browser(url: &str) {
    #[cfg(windows)]
    let result = std::process::Command::new("rundll32").args(["url.dll,FileProtocolHandler", url]).spawn();
    #[cfg(target_os = "macos")]
    let result = std::process::Command::new("open").arg(url).spawn();
    #[cfg(all(unix, not(target_os = "macos")))]
    let result = std::process::Command::new("xdg-open").arg(url).spawn();
    if let Err(error) = result {
        tracing::warn!("tarayıcı açılamadı: {error}");
    }
}

async fn accept(listener: &TcpListener, listener6: &Option<TcpListener>) -> std::io::Result<TcpStream> {
    match listener6 {
        Some(l6) => tokio::select! {
            r = listener.accept() => r.map(|(s, _)| s),
            r = l6.accept() => r.map(|(s, _)| s),
        },
        None => listener.accept().await.map(|(s, _)| s),
    }
}

/// Tarayıcının dönüşünü bekler; izin kodunu anahtara çevirir, takvimi açar, hesabı kaydeder.
#[allow(clippy::too_many_arguments)]
async fn wait_for_callback(
    app: &App,
    provider: Provider,
    cfg: &ProviderConfig,
    listener: TcpListener,
    listener6: Option<TcpListener>,
    state: &str,
    verifier: &str,
    redirect: &str,
    user_id: &str,
) -> Result<(), String> {
    loop {
        let mut stream = accept(&listener, &listener6).await.map_err(|e| e.to_string())?;
        let mut buf = vec![0u8; 8192];
        let n = tokio::time::timeout(Duration::from_secs(10), stream.read(&mut buf)).await.unwrap_or(Ok(0)).unwrap_or(0);
        let head = String::from_utf8_lossy(&buf[..n]);
        let path = head.lines().next().and_then(|l| l.split_whitespace().nth(1)).unwrap_or("/");
        let url = match reqwest::Url::parse(&format!("http://127.0.0.1{path}")) {
            Ok(u) => u,
            Err(_) => {
                respond(&mut stream, 400, "Geçersiz istek.").await;
                continue;
            }
        };
        let get = |key: &str| url.query_pairs().find(|(k, _)| k == key).map(|(_, v)| v.into_owned());
        // favicon gibi diğer istekler.
        if get("state").is_none() {
            respond(&mut stream, 404, "").await;
            continue;
        }
        if get("state").as_deref() != Some(state) {
            respond(&mut stream, 400, "Bu bağlantı eski. Salonera'da tekrar \"Bağla\"ya basın.").await;
            continue;
        }
        if let Some(error) = get("error") {
            let message = if error == "access_denied" { "İzin verilmedi.".to_string() } else { format!("{} izin vermedi: {error}", provider.label()) };
            respond(&mut stream, 200, &message).await;
            return Err(message);
        }
        let Some(code) = get("code") else {
            respond(&mut stream, 400, "İzin kodu gelmedi.").await;
            return Err("İzin kodu gelmedi.".into());
        };
        let result = complete(app, provider, cfg, &code, verifier, redirect, user_id).await;
        match &result {
            Ok(email) => {
                respond(&mut stream, 200, &format!("{email} hesabı Salonera'ya bağlandı. Bu pencereyi kapatıp Salonera'ya dönebilirsiniz.")).await
            }
            Err(message) => respond(&mut stream, 200, &format!("Bağlantı tamamlanamadı: {message}")).await,
        }
        return result.map(|_| ());
    }
}

async fn complete(
    app: &App,
    provider: Provider,
    cfg: &ProviderConfig,
    code: &str,
    verifier: &str,
    redirect: &str,
    user_id: &str,
) -> Result<String, String> {
    let http = app.calsync.http();
    let tokens = remote::exchange_code(http, provider, cfg, code, verifier, redirect).await.map_err(|e| e.to_string())?;
    let refresh_token = tokens.refresh_token.clone().ok_or("Hizmet kalıcı izin anahtarı vermedi; tekrar deneyin.")?;
    let api = Remote { http, provider, cfg, token: tokens.access_token.clone() };
    let email = api.email().await.map_err(|e| e.to_string())?;
    let business = app
        .with_db(|c| Ok(c.query_row("SELECT business_name FROM settings WHERE id = 1", [], |r| r.get::<_, String>(0)).unwrap_or_default()))
        .unwrap_or_default();
    let name = if business.trim().is_empty() { "Salonera".to_string() } else { format!("Salonera – {}", business.trim()) };

    // Aynı hesap daha önce bağlandıysa eski takvimini kaldırıp yenisiyle değiştir (çift etkinlik olmasın).
    let old: Vec<(String, String)> = app
        .with_db(|c| {
            let mut s = c.prepare("SELECT id, calendar_id FROM calendar_accounts WHERE provider = ?1 AND email = ?2")?;
            let rows = s.query_map([provider.as_str(), email.as_str()], |r| Ok((r.get(0)?, r.get(1)?)))?.collect::<Result<_, _>>()?;
            Ok(rows)
        })
        .map_err(|e| e.to_string())?;
    for (_, calendar) in &old {
        let _ = api.delete_calendar(calendar).await;
    }
    let calendar = match api.create_calendar(&name).await {
        Ok(id) => id,
        Err(RemoteError::Auth(m)) | Err(RemoteError::Failed(m)) => return Err(format!("Takvim açılamadı: {m}")),
        Err(RemoteError::NotFound) => return Err("Takvim açılamadı.".into()),
    };
    let now = fmt_datetime(app.now());
    app.with_db(|c| {
        let tx = c.transaction()?;
        for (id, _) in &old {
            tx.execute("DELETE FROM calendar_accounts WHERE id = ?1", [id])?;
        }
        tx.execute(
            "INSERT INTO calendar_accounts (id, provider, email, refresh_token, access_token, access_expires, calendar_id,
                                            calendar_name, connected_at, connected_by)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10)",
            rusqlite::params![
                new_id(),
                provider.as_str(),
                email,
                refresh_token,
                tokens.access_token,
                unix_now() + tokens.expires_in,
                calendar,
                name,
                now,
                user_id
            ],
        )?;
        tx.execute(
            "INSERT INTO audit (at, user_id, action, detail) VALUES (?1, ?2, 'takvim_baglandi', ?3)",
            rusqlite::params![now, user_id, format!("{} {}", provider.label(), email)],
        )?;
        tx.commit()?;
        Ok(())
    })
    .map_err(|e| e.to_string())?;
    app.calsync.changed();
    Ok(email)
}

async fn respond(stream: &mut TcpStream, status: u16, message: &str) {
    let body = if message.is_empty() {
        String::new()
    } else {
        let safe = message.replace('&', "&amp;").replace('<', "&lt;").replace('>', "&gt;");
        format!(
            "<!doctype html><html lang=\"tr\"><head><meta charset=\"utf-8\"><title>Salonera</title></head>\
             <body style=\"font-family:Segoe UI,Arial,sans-serif;background:#f8f1ec;color:#3b1a2a;display:flex;\
             align-items:center;justify-content:center;height:100vh;margin:0\"><div style=\"background:#fff;\
             padding:36px 44px;border-radius:16px;box-shadow:0 8px 30px rgba(90,29,60,.15);max-width:520px\">\
             <h2 style=\"margin:0 0 12px;color:#8a2846\">Salonera takvim bağlantısı</h2><p style=\"font-size:17px;\
             line-height:1.5\">{safe}</p></div></body></html>"
        )
    };
    let reason = match status {
        200 => "OK",
        404 => "Not Found",
        _ => "Bad Request",
    };
    let response = format!(
        "HTTP/1.1 {status} {reason}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",
        body.len()
    );
    let _ = stream.write_all(response.as_bytes()).await;
    let _ = stream.shutdown().await;
}
