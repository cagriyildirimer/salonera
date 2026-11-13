//! Google Takvim ve Microsoft Graph (Outlook) ile konuşan kısım. Yalnızca Salonera'nın kendi açtığı
//! takvimdeki etkinliklere dokunulur.

use std::time::Duration;

use reqwest::{Client, RequestBuilder, StatusCode};
use serde::Deserialize;
use serde_json::{json, Value};

use super::event::CalendarEvent;
use super::{Provider, ProviderConfig};

#[derive(Debug)]
pub enum RemoteError {
    /// İzin geri alınmış / süresi dolmuş: hesabın yeniden bağlanması gerekir.
    Auth(String),
    /// Etkinlik veya takvim bulunamadı (takvimden elle silinmiş).
    NotFound,
    Failed(String),
}

impl std::fmt::Display for RemoteError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            RemoteError::Auth(m) | RemoteError::Failed(m) => f.write_str(m),
            RemoteError::NotFound => f.write_str("takvimde bulunamadı"),
        }
    }
}

pub type RemoteResult<T> = Result<T, RemoteError>;

/// Google: salon sırasına göre etkinlik rengi (üzüm, muz, tavus, fesleğen, flamingo, yaban mersini, mandalina, adaçayı).
const GOOGLE_HALL_COLORS: [&str; 8] = ["3", "5", "7", "10", "4", "9", "6", "2"];
/// Ön rezervasyon gri (grafit) görünür.
const GOOGLE_OPTION_COLOR: &str = "8";
const GOOGLE_TZ: &str = "Europe/Istanbul";
const WINDOWS_TZ: &str = "Turkey Standard Time";

/// Yol parçası olarak güvenli yazım (takvim ve etkinlik kimliklerinde @, =, / olabilir).
fn seg(value: &str) -> String {
    value
        .bytes()
        .map(|b| match b {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => (b as char).to_string(),
            _ => format!("%{b:02X}"),
        })
        .collect()
}

fn error_message(body: &Value) -> String {
    body["error"]["message"]
        .as_str()
        .or_else(|| body["error_description"].as_str())
        .or_else(|| body["error"].as_str())
        .unwrap_or("bilinmeyen hata")
        .to_string()
}

fn is_rate_limited(status: StatusCode, body: &Value) -> bool {
    if matches!(status.as_u16(), 429 | 500 | 502 | 503 | 504) {
        return true;
    }
    // Google kota aşımını 403 ile bildirir.
    status == StatusCode::FORBIDDEN && body.to_string().contains("ateLimitExceeded")
}

/// İsteği gönderir; kota aşımı ve geçici sunucu hatalarında birkaç kez bekleyip yeniden dener.
async fn send(build: impl Fn() -> RequestBuilder) -> RemoteResult<Value> {
    let mut wait = Duration::from_millis(800);
    for attempt in 0..5 {
        let response = match build().send().await {
            Ok(r) => r,
            Err(e) if attempt < 4 && (e.is_timeout() || e.is_connect()) => {
                tokio::time::sleep(wait).await;
                wait *= 2;
                continue;
            }
            Err(e) => return Err(RemoteError::Failed(format!("İnternet bağlantısı kurulamadı ({e})"))),
        };
        let status = response.status();
        let retry_after = response
            .headers()
            .get("retry-after")
            .and_then(|v| v.to_str().ok())
            .and_then(|v| v.parse::<u64>().ok());
        let text = response.text().await.unwrap_or_default();
        let body: Value = if text.trim().is_empty() { Value::Null } else { serde_json::from_str(&text).unwrap_or(Value::Null) };
        if status.is_success() {
            return Ok(body);
        }
        if status == StatusCode::UNAUTHORIZED {
            return Err(RemoteError::Auth(error_message(&body)));
        }
        if status == StatusCode::NOT_FOUND || status == StatusCode::GONE {
            return Err(RemoteError::NotFound);
        }
        if attempt < 4 && is_rate_limited(status, &body) {
            let pause = retry_after.map(|s| Duration::from_secs(s.min(30))).unwrap_or(wait);
            tokio::time::sleep(pause).await;
            wait *= 2;
            continue;
        }
        return Err(RemoteError::Failed(format!("{} ({})", error_message(&body), status.as_u16())));
    }
    Err(RemoteError::Failed("Takvim hizmeti şu an yanıt vermiyor.".into()))
}

// ---------------- Giriş anahtarları (OAuth) ----------------

#[derive(Deserialize)]
pub struct Tokens {
    pub access_token: String,
    pub refresh_token: Option<String>,
    #[serde(default = "default_expiry")]
    pub expires_in: i64,
}

fn default_expiry() -> i64 {
    3600
}

pub fn scopes(provider: Provider) -> &'static str {
    match provider {
        // Yalnızca Salonera'nın açtığı takvim; kişinin diğer takvimleri görülmez.
        Provider::Google => "openid email https://www.googleapis.com/auth/calendar.app.created",
        Provider::Microsoft => "offline_access User.Read Calendars.ReadWrite",
    }
}

async fn token_request(http: &Client, cfg: &ProviderConfig, mut form: Vec<(&str, String)>) -> RemoteResult<Tokens> {
    form.push(("client_id", cfg.client_id.clone()));
    if let Some(secret) = &cfg.client_secret {
        form.push(("client_secret", secret.clone()));
    }
    let response = http
        .post(&cfg.token_url)
        .form(&form)
        .send()
        .await
        .map_err(|e| RemoteError::Failed(format!("İnternet bağlantısı kurulamadı ({e})")))?;
    let status = response.status();
    let body: Value = response.json().await.unwrap_or(Value::Null);
    if status.is_success() {
        return serde_json::from_value(body).map_err(|e| RemoteError::Failed(format!("beklenmeyen yanıt: {e}")));
    }
    let code = body["error"].as_str().unwrap_or_default();
    if status == StatusCode::BAD_REQUEST && (code == "invalid_grant" || code == "interaction_required") || status == StatusCode::UNAUTHORIZED {
        return Err(RemoteError::Auth(error_message(&body)));
    }
    Err(RemoteError::Failed(format!("{} ({})", error_message(&body), status.as_u16())))
}

pub async fn exchange_code(
    http: &Client,
    provider: Provider,
    cfg: &ProviderConfig,
    code: &str,
    verifier: &str,
    redirect: &str,
) -> RemoteResult<Tokens> {
    let mut form = vec![
        ("grant_type", "authorization_code".to_string()),
        ("code", code.to_string()),
        ("code_verifier", verifier.to_string()),
        ("redirect_uri", redirect.to_string()),
    ];
    if provider == Provider::Microsoft {
        form.push(("scope", scopes(provider).to_string()));
    }
    token_request(http, cfg, form).await
}

pub async fn refresh(http: &Client, provider: Provider, cfg: &ProviderConfig, refresh_token: &str) -> RemoteResult<Tokens> {
    let mut form = vec![("grant_type", "refresh_token".to_string()), ("refresh_token", refresh_token.to_string())];
    if provider == Provider::Microsoft {
        form.push(("scope", scopes(provider).to_string()));
    }
    token_request(http, cfg, form).await
}

// ---------------- Takvim işlemleri ----------------

pub struct Remote<'a> {
    pub http: &'a Client,
    pub provider: Provider,
    pub cfg: &'a ProviderConfig,
    pub token: String,
}

impl Remote<'_> {
    fn url(&self, path: &str) -> String {
        format!("{}{}", self.cfg.api_base.trim_end_matches('/'), path)
    }

    fn req(&self, method: reqwest::Method, path: &str) -> RequestBuilder {
        self.http.request(method, self.url(path)).bearer_auth(&self.token)
    }

    /// Bağlanan hesabın e-posta adresi (ekranda hangi hesabın bağlı olduğunu göstermek için).
    pub async fn email(&self) -> RemoteResult<String> {
        match self.provider {
            Provider::Google => {
                let body = send(|| self.req(reqwest::Method::GET, "/oauth2/v3/userinfo")).await?;
                Ok(body["email"].as_str().unwrap_or("Google hesabı").to_string())
            }
            Provider::Microsoft => {
                let body = send(|| self.req(reqwest::Method::GET, "/me")).await?;
                Ok(body["mail"].as_str().or(body["userPrincipalName"].as_str()).unwrap_or("Microsoft hesabı").to_string())
            }
        }
    }

    /// Salonera'nın etkinlikleri yazacağı ayrı takvimi açar; kimliğini döndürür.
    pub async fn create_calendar(&self, name: &str) -> RemoteResult<String> {
        match self.provider {
            Provider::Google => {
                let body = json!({ "summary": name, "timeZone": GOOGLE_TZ, "description": "Salonera rezervasyonları (otomatik)" });
                let r = send(|| self.req(reqwest::Method::POST, "/calendar/v3/calendars").json(&body)).await?;
                r["id"].as_str().map(String::from).ok_or_else(|| RemoteError::Failed("takvim açılamadı".into()))
            }
            Provider::Microsoft => {
                let body = json!({ "name": name });
                match send(|| self.req(reqwest::Method::POST, "/me/calendars").json(&body)).await {
                    Ok(r) => r["id"].as_str().map(String::from).ok_or_else(|| RemoteError::Failed("takvim açılamadı".into())),
                    // Aynı adda takvim zaten varsa (daha önce bağlanmış) onu kullan.
                    Err(RemoteError::Failed(m)) if m.contains("(409)") => {
                        let list = send(|| self.req(reqwest::Method::GET, "/me/calendars?$top=100")).await?;
                        list["value"]
                            .as_array()
                            .and_then(|items| items.iter().find(|c| c["name"].as_str() == Some(name)))
                            .and_then(|c| c["id"].as_str())
                            .map(String::from)
                            .ok_or(RemoteError::Failed(m))
                    }
                    Err(e) => Err(e),
                }
            }
        }
    }

    pub async fn delete_calendar(&self, calendar: &str) -> RemoteResult<()> {
        let path = match self.provider {
            Provider::Google => format!("/calendar/v3/calendars/{}", seg(calendar)),
            Provider::Microsoft => format!("/me/calendars/{}", seg(calendar)),
        };
        match send(|| self.req(reqwest::Method::DELETE, &path)).await {
            Ok(_) | Err(RemoteError::NotFound) => Ok(()),
            Err(e) => Err(e),
        }
    }

    fn body(&self, reservation_id: &str, e: &CalendarEvent) -> Value {
        let at = |t: chrono::NaiveDateTime, tz: &str| json!({ "dateTime": t.format("%Y-%m-%dT%H:%M:%S").to_string(), "timeZone": tz });
        match self.provider {
            Provider::Google => json!({
                "summary": e.title,
                "description": e.description,
                "location": e.location,
                "start": at(e.start, GOOGLE_TZ),
                "end": at(e.end, GOOGLE_TZ),
                "status": if e.tentative { "tentative" } else { "confirmed" },
                "colorId": if e.tentative { GOOGLE_OPTION_COLOR } else { GOOGLE_HALL_COLORS[e.hall_index % GOOGLE_HALL_COLORS.len()] },
                "extendedProperties": { "private": { "saloneraId": reservation_id } },
            }),
            Provider::Microsoft => json!({
                "subject": e.title,
                "body": { "contentType": "text", "content": e.description },
                "location": { "displayName": e.location },
                "start": at(e.start, WINDOWS_TZ),
                "end": at(e.end, WINDOWS_TZ),
                "showAs": if e.tentative { "tentative" } else { "busy" },
                "isReminderOn": false,
            }),
        }
    }

    pub async fn create_event(&self, calendar: &str, reservation_id: &str, e: &CalendarEvent) -> RemoteResult<String> {
        let body = self.body(reservation_id, e);
        let path = match self.provider {
            Provider::Google => format!("/calendar/v3/calendars/{}/events", seg(calendar)),
            Provider::Microsoft => format!("/me/calendars/{}/events", seg(calendar)),
        };
        let r = send(|| self.req(reqwest::Method::POST, &path).json(&body)).await?;
        r["id"].as_str().map(String::from).ok_or_else(|| RemoteError::Failed("etkinlik eklenemedi".into()))
    }

    pub async fn update_event(&self, calendar: &str, event_id: &str, reservation_id: &str, e: &CalendarEvent) -> RemoteResult<()> {
        let body = self.body(reservation_id, e);
        match self.provider {
            Provider::Google => {
                let path = format!("/calendar/v3/calendars/{}/events/{}", seg(calendar), seg(event_id));
                send(|| self.req(reqwest::Method::PUT, &path).json(&body)).await?;
            }
            Provider::Microsoft => {
                let path = format!("/me/events/{}", seg(event_id));
                send(|| self.req(reqwest::Method::PATCH, &path).json(&body)).await?;
            }
        }
        Ok(())
    }

    pub async fn delete_event(&self, calendar: &str, event_id: &str) -> RemoteResult<()> {
        let path = match self.provider {
            Provider::Google => format!("/calendar/v3/calendars/{}/events/{}", seg(calendar), seg(event_id)),
            Provider::Microsoft => format!("/me/events/{}", seg(event_id)),
        };
        match send(|| self.req(reqwest::Method::DELETE, &path)).await {
            Ok(_) | Err(RemoteError::NotFound) => Ok(()),
            Err(e) => Err(e),
        }
    }
}

#[cfg(test)]
mod tests {
    #[test]
    fn path_segments_are_escaped() {
        assert_eq!(super::seg("abc@group.calendar.google.com"), "abc%40group.calendar.google.com");
        assert_eq!(super::seg("AAMk/x=="), "AAMk%2Fx%3D%3D");
    }
}
