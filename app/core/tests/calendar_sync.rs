//! Takvim eşitleme uçtan uca: sahte Google Takvim ve Microsoft Graph sunucusuyla gerçek bağlanma akışı
//! (tarayıcının geri dönüşü dahil), ekleme, güncelleme, iptal, opsiyon süresinin dolması, elle silinen
//! etkinlik, izin yenileme ve izni geri alınan hesap.

use std::collections::HashMap;
use std::sync::{Arc, Mutex};

use axum::body::Body;
use axum::extract::{Form, Path, State};
use axum::http::{HeaderMap, Method, Request, StatusCode};
use axum::response::IntoResponse;
use axum::routing::{delete, get, patch, post, put};
use axum::{Json, Router};
use chrono::{NaiveDate, NaiveDateTime};
use http_body_util::BodyExt;
use salonera_core::calsync::{self, ProviderConfig, SyncConfig};
use salonera_core::{server, App};
use serde_json::{json, Value};
use tower::ServiceExt;

// ---------------- Sahte takvim hizmeti ----------------

#[derive(Default)]
struct Fake {
    /// Etkinlik kimliği → (takvim, gövde)
    events: HashMap<String, (String, Value)>,
    calendars: Vec<String>,
    deleted_calendars: Vec<String>,
    next: usize,
    /// Geçerli erişim anahtarı; başka anahtarla gelen istek 401 alır.
    token: String,
    refreshes: usize,
    revoked: bool,
    /// İlk anahtar hemen dolsun ki yenileme denensin.
    expires_in: i64,
}

type Shared = Arc<Mutex<Fake>>;

fn authorized(fake: &Fake, headers: &HeaderMap) -> bool {
    headers.get("authorization").and_then(|v| v.to_str().ok()) == Some(&format!("Bearer {}", fake.token))
}

async fn token(State(s): State<Shared>, Form(form): Form<HashMap<String, String>>) -> axum::response::Response {
    let mut f = s.lock().unwrap();
    if f.revoked {
        return (StatusCode::BAD_REQUEST, Json(json!({"error": "invalid_grant", "error_description": "Token has been revoked."}))).into_response();
    }
    assert!(form.contains_key("client_id"));
    match form["grant_type"].as_str() {
        "authorization_code" => {
            assert_eq!(form["code"], "izin-kodu");
            assert!(form.get("code_verifier").is_some_and(|v| v.len() >= 43));
            assert!(form["redirect_uri"].starts_with("http://"));
        }
        "refresh_token" => {
            assert_eq!(form["refresh_token"], "kalici");
            f.refreshes += 1;
        }
        other => panic!("beklenmeyen grant_type {other}"),
    }
    f.next += 1;
    f.token = format!("erisim-{}", f.next);
    let expires = f.expires_in;
    f.expires_in = 3600;
    Json(json!({"access_token": f.token, "refresh_token": "kalici", "expires_in": expires})).into_response()
}

async fn email(State(s): State<Shared>, headers: HeaderMap) -> axum::response::Response {
    let f = s.lock().unwrap();
    if !authorized(&f, &headers) {
        return StatusCode::UNAUTHORIZED.into_response();
    }
    Json(json!({"email": "salon@example.com", "mail": "salon@example.com"})).into_response()
}

async fn new_calendar(State(s): State<Shared>, headers: HeaderMap, Json(body): Json<Value>) -> axum::response::Response {
    let mut f = s.lock().unwrap();
    if !authorized(&f, &headers) {
        return StatusCode::UNAUTHORIZED.into_response();
    }
    assert!(body["summary"].as_str().or(body["name"].as_str()).unwrap().starts_with("Salonera – "));
    f.next += 1;
    let id = format!("takvim{}@group.calendar.google.com", f.next);
    f.calendars.push(id.clone());
    Json(json!({"id": id})).into_response()
}

async fn drop_calendar(State(s): State<Shared>, headers: HeaderMap, Path(cal): Path<String>) -> StatusCode {
    let mut f = s.lock().unwrap();
    if !authorized(&f, &headers) {
        return StatusCode::UNAUTHORIZED;
    }
    f.calendars.retain(|c| c != &cal);
    f.events.retain(|_, (c, _)| c != &cal);
    f.deleted_calendars.push(cal);
    StatusCode::NO_CONTENT
}

async fn new_event(State(s): State<Shared>, headers: HeaderMap, Path(cal): Path<String>, Json(body): Json<Value>) -> axum::response::Response {
    let mut f = s.lock().unwrap();
    if !authorized(&f, &headers) {
        return StatusCode::UNAUTHORIZED.into_response();
    }
    if !f.calendars.contains(&cal) {
        return StatusCode::NOT_FOUND.into_response();
    }
    f.next += 1;
    let id = format!("etkinlik/{}=", f.next);
    f.events.insert(id.clone(), (cal, body));
    Json(json!({"id": id})).into_response()
}

async fn change_event(State(s): State<Shared>, headers: HeaderMap, Path(path): Path<Vec<String>>, Json(body): Json<Value>) -> StatusCode {
    let mut f = s.lock().unwrap();
    if !authorized(&f, &headers) {
        return StatusCode::UNAUTHORIZED;
    }
    let id = path.last().unwrap().clone();
    match f.events.get_mut(&id) {
        Some((_, old)) => {
            *old = body;
            StatusCode::OK
        }
        None => StatusCode::NOT_FOUND,
    }
}

async fn remove_event(State(s): State<Shared>, headers: HeaderMap, Path(path): Path<Vec<String>>) -> StatusCode {
    let mut f = s.lock().unwrap();
    if !authorized(&f, &headers) {
        return StatusCode::UNAUTHORIZED;
    }
    match f.events.remove(path.last().unwrap()) {
        Some(_) => StatusCode::NO_CONTENT,
        None => StatusCode::GONE,
    }
}

/// Google ve Microsoft uçlarını aynı sahte sunucuda taklit eder.
async fn fake_server() -> (String, Shared) {
    let shared: Shared = Arc::new(Mutex::new(Fake { expires_in: 1, ..Default::default() }));
    let app = Router::new()
        .route("/token", post(token))
        // Google
        .route("/oauth2/v3/userinfo", get(email))
        .route("/calendar/v3/calendars", post(new_calendar))
        .route("/calendar/v3/calendars/{cal}", delete(drop_calendar))
        .route("/calendar/v3/calendars/{cal}/events", post(new_event))
        .route("/calendar/v3/calendars/{cal}/events/{id}", put(change_event).delete(remove_event))
        // Microsoft Graph
        .route("/me", get(email))
        .route("/me/calendars", post(new_calendar))
        .route("/me/calendars/{cal}", delete(drop_calendar))
        .route("/me/calendars/{cal}/events", post(new_event))
        .route("/me/events/{id}", patch(change_event).delete(remove_event))
        .with_state(shared.clone());
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0").await.unwrap();
    let base = format!("http://{}", listener.local_addr().unwrap());
    tokio::spawn(async move { axum::serve(listener, app).await.unwrap() });
    (base, shared)
}

fn provider(base: &str) -> ProviderConfig {
    ProviderConfig {
        client_id: "salonera-test".into(),
        client_secret: Some("gizli".into()),
        auth_url: format!("{base}/auth"),
        token_url: format!("{base}/token"),
        api_base: base.to_string(),
    }
}

// ---------------- Salonera tarafı ----------------

struct Salon {
    app: Arc<App>,
    router: Router,
    clock: Arc<Mutex<NaiveDateTime>>,
    token: String,
}

impl Salon {
    async fn new(base: &str) -> Salon {
        let start = NaiveDate::from_ymd_opt(2026, 3, 2).unwrap().and_hms_opt(10, 0, 0).unwrap();
        let clock = Arc::new(Mutex::new(start));
        let c = clock.clone();
        let app = App::in_memory(Arc::new(move || *c.lock().unwrap())).unwrap();
        app.calsync.configure(SyncConfig { google: Some(provider(base)), microsoft: Some(provider(base)), open_browser: false, demo: false });
        let mut salon = Salon { router: server::router(app.clone()), app, clock, token: String::new() };
        salon
            .ok(Method::POST, "/api/setup", json!({
                "businessName": "Kristal Düğün Salonu", "phone": "0212 555 00 00", "adminName": "Ayşe Yönetici",
                "adminUsername": "ayse", "adminPassword": "Salon2026",
                "halls": [{"name": "Kristal Salon", "capacity": 400}, {"name": "Bahçe", "capacity": 200}]
            }))
            .await;
        let login = salon.ok(Method::POST, "/api/login", json!({"username": "ayse", "password": "Salon2026"})).await;
        salon.token = login["token"].as_str().unwrap().to_string();
        salon
    }

    async fn call(&self, method: Method, path: &str, body: Value) -> (StatusCode, Value) {
        let mut request = Request::builder().method(method).uri(path).header("content-type", "application/json");
        if !self.token.is_empty() {
            request = request.header("authorization", format!("Bearer {}", self.token));
        }
        let request = request.body(if body.is_null() { Body::empty() } else { Body::from(body.to_string()) }).unwrap();
        let response = self.router.clone().oneshot(request).await.unwrap();
        let status = response.status();
        let bytes = response.into_body().collect().await.unwrap().to_bytes();
        (status, if bytes.is_empty() { Value::Null } else { serde_json::from_slice(&bytes).unwrap_or(Value::Null) })
    }

    async fn ok(&self, method: Method, path: &str, body: Value) -> Value {
        let (status, response) = self.call(method, path, body).await;
        assert_eq!(status, StatusCode::OK, "{path}: {response}");
        response
    }

    async fn status(&self) -> Value {
        self.ok(Method::GET, "/api/calendar-sync", Value::Null).await
    }

    /// "Bağla"ya basılır, kullanıcı tarayıcıda izin verir (tarayıcının geri dönüşü taklit edilir).
    async fn connect(&self, provider: &str) {
        let r = self.ok(Method::POST, "/api/calendar-sync/connect", json!({"provider": provider})).await;
        let auth = reqwest::Url::parse(r["authUrl"].as_str().unwrap()).unwrap();
        let q: HashMap<String, String> = auth.query_pairs().into_owned().collect();
        assert_eq!(q["code_challenge_method"], "S256");
        if provider == "google" {
            assert!(q["scope"].contains("calendar.app.created"));
            assert_eq!(q["access_type"], "offline");
        } else {
            assert!(q["scope"].contains("Calendars.ReadWrite") && q["scope"].contains("offline_access"));
        }
        let back = format!("{}?code=izin-kodu&state={}", q["redirect_uri"], q["state"]).replace("localhost", "127.0.0.1");
        let page = reqwest::get(&back).await.unwrap().text().await.unwrap();
        assert!(page.contains("salon@example.com hesabı Salonera'ya bağlandı"), "{page}");
        assert!(self.status().await["pending"].is_null());
    }

    fn set_today(&self, date: &str) {
        *self.clock.lock().unwrap() = NaiveDate::parse_from_str(date, "%Y-%m-%d").unwrap().and_hms_opt(10, 0, 0).unwrap();
    }
}

fn events(fake: &Shared) -> Vec<Value> {
    let f = fake.lock().unwrap();
    let mut list: Vec<Value> = f.events.values().map(|(_, b)| b.clone()).collect();
    list.sort_by_key(|b| b["summary"].as_str().or(b["subject"].as_str()).unwrap_or_default().to_string());
    list
}

#[tokio::test(flavor = "multi_thread")]
async fn reservations_flow_one_way_into_google_calendar() {
    let (base, fake) = fake_server().await;
    let salon = Salon::new(&base).await;
    let st = salon.status().await;
    assert_eq!(st["googleAvailable"], true);
    assert_eq!(st["accounts"], json!([]));

    let halls = salon.ok(Method::GET, "/api/halls", Value::Null).await;
    let kristal = halls[0]["id"].as_str().unwrap().to_string();
    let bahce = halls[1]["id"].as_str().unwrap().to_string();
    let gold = salon.ok(Method::POST, "/api/packages", json!({"name": "Altın Paket", "price": 200_000_00, "isActive": true})).await;
    let wedding = salon
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": {"fullName": "İpek Işık", "phone": "0532 111 22 33", "brideName": "Zeynep", "groomName": "Emre"},
            "hallId": kristal, "date": "2026-09-12", "start": "19:00", "end": "00:00", "kind": "dugun", "guestCount": 350,
            "sale": {"packageId": gold, "firstPayment": {"amount": 50_000_00, "method": "nakit"}}
        }))
        .await["reservationId"]
        .as_str()
        .unwrap()
        .to_string();
    let option = salon
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": {"fullName": "Nişan Ailesi", "phone": "0533 000 00 00"},
            "hallId": bahce, "date": "2026-05-20", "start": "15:00", "end": "18:00", "kind": "nisan", "guestCount": 120, "optionDays": 7
        }))
        .await["reservationId"]
        .as_str()
        .unwrap()
        .to_string();

    // Hesap bağlanınca var olan kayıtlar takvime yazılır. İlk erişim anahtarı hemen dolduğu için yenilenir.
    salon.connect("google").await;
    calsync::sync_all(&salon.app).await;
    let list = events(&fake);
    assert_eq!(list.len(), 2, "{list:?}");
    assert_eq!(fake.lock().unwrap().refreshes, 1);
    let opt = &list[0];
    assert_eq!(opt["summary"], "(Opsiyon) Nişan · Nişan Ailesi — Bahçe");
    assert_eq!(opt["status"], "tentative");
    assert_eq!(opt["colorId"], "8");
    let wed = &list[1];
    assert_eq!(wed["summary"], "Düğün · Zeynep & Emre — Kristal Salon");
    assert_eq!(wed["start"], json!({"dateTime": "2026-09-12T19:00:00", "timeZone": "Europe/Istanbul"}));
    assert_eq!(wed["end"]["dateTime"], "2026-09-13T00:00:00");
    assert_eq!(wed["location"], "Kristal Salon, Kristal Düğün Salonu");
    let text = wed["description"].as_str().unwrap();
    assert!(text.contains("Telefon: 0532 111 22 33") && text.contains("Paket: Altın Paket") && text.contains("Davetli: 350 kişi"));
    assert!(!text.contains("200") && !text.contains("TL") && !text.contains("₺"), "para bilgisi takvime gitmemeli: {text}");
    assert_eq!(wed["extendedProperties"]["private"]["saloneraId"], wedding.as_str());

    let st = salon.status().await;
    assert_eq!(st["accounts"][0]["email"], "salon@example.com");
    assert_eq!(st["accounts"][0]["eventCount"], 2);
    assert_eq!(st["accounts"][0]["lastError"], Value::Null);

    // Değişiklik yoksa takvime tekrar yazılmaz.
    let before = fake.lock().unwrap().next;
    calsync::sync_all(&salon.app).await;
    assert_eq!(fake.lock().unwrap().next, before);

    // Saat ve davetli değişir → aynı etkinlik güncellenir.
    salon
        .ok(Method::PUT, &format!("/api/reservations/{wedding}"), json!({
            "hallId": kristal, "date": "2026-09-12", "start": "20:00", "end": "00:30", "kind": "dugun", "guestCount": 360, "notes": null
        }))
        .await;
    calsync::sync_all(&salon.app).await;
    let list = events(&fake);
    assert_eq!(list.len(), 2);
    assert_eq!(list[1]["start"]["dateTime"], "2026-09-12T20:00:00");
    assert!(list[1]["description"].as_str().unwrap().contains("Davetli: 360 kişi"));

    // Biri takvimden etkinliği elle silerse, kayıt bir sonraki değişiklikte geri gelir.
    fake.lock().unwrap().events.retain(|_, (_, b)| b["extendedProperties"]["private"]["saloneraId"] != wedding.as_str());
    salon
        .ok(Method::PUT, &format!("/api/reservations/{wedding}"), json!({
            "hallId": kristal, "date": "2026-09-12", "start": "20:00", "end": "00:30", "kind": "dugun", "guestCount": 370, "notes": null
        }))
        .await;
    calsync::sync_all(&salon.app).await;
    assert_eq!(events(&fake).len(), 2);

    // Opsiyon süresi dolunca takvimden silinir.
    salon.set_today("2026-03-10");
    calsync::sync_all(&salon.app).await;
    let list = events(&fake);
    assert_eq!(list.len(), 1);
    assert_eq!(list[0]["summary"], "Düğün · Zeynep & Emre — Kristal Salon");

    // İptal edilen kayıt silinir.
    salon.ok(Method::POST, &format!("/api/reservations/{wedding}/cancel"), json!({"reason": "Aile vazgeçti"})).await;
    calsync::sync_all(&salon.app).await;
    assert!(events(&fake).is_empty());
    assert_eq!(salon.status().await["accounts"][0]["eventCount"], 0);
    let _ = option;

    // İzin Google hesabından geri alınırsa ekranda "yeniden bağlayın" çıkar; eşitleme durur.
    {
        let mut f = fake.lock().unwrap();
        f.revoked = true;
        f.token = "gecersiz".into();
    }
    salon
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": {"fullName": "Yeni Müşteri", "phone": "0534 000 00 00"},
            "hallId": bahce, "date": "2026-06-01", "start": "12:00", "end": "15:00", "kind": "soz", "guestCount": 80, "optionDays": 7
        }))
        .await;
    calsync::sync_all(&salon.app).await;
    let acc = &salon.status().await["accounts"][0];
    assert_eq!(acc["needsReconnect"], true);
    assert!(acc["lastError"].as_str().unwrap().contains("yeniden bağlayın"));
}

#[tokio::test(flavor = "multi_thread")]
async fn outlook_account_and_disconnect_removes_salonera_calendar() {
    let (base, fake) = fake_server().await;
    let salon = Salon::new(&base).await;
    let hall = salon.ok(Method::GET, "/api/halls", Value::Null).await[0]["id"].as_str().unwrap().to_string();
    salon
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": {"fullName": "Deniz Ak", "phone": "0535 000 00 00"},
            "hallId": hall, "date": "2026-04-18", "start": "18:00", "end": "23:00", "kind": "kina", "guestCount": 200, "optionDays": 5
        }))
        .await;
    salon.connect("microsoft").await;
    calsync::sync_all(&salon.app).await;
    let list = events(&fake);
    assert_eq!(list.len(), 1);
    assert_eq!(list[0]["subject"], "(Opsiyon) Kına · Deniz Ak — Kristal Salon");
    assert_eq!(list[0]["showAs"], "tentative");
    assert_eq!(list[0]["start"], json!({"dateTime": "2026-04-18T18:00:00", "timeZone": "Turkey Standard Time"}));

    // Etkinliği Salonera takvimiyle birlikte silinen bağlantı.
    let account = salon.status().await["accounts"][0]["id"].as_str().unwrap().to_string();
    salon.ok(Method::DELETE, &format!("/api/calendar-sync/accounts/{account}"), Value::Null).await;
    assert_eq!(salon.status().await["accounts"], json!([]));
    let f = fake.lock().unwrap();
    assert_eq!(f.deleted_calendars.len(), 1);
    assert!(f.events.is_empty());
}

#[tokio::test(flavor = "multi_thread")]
async fn only_admins_manage_calendar_sync_and_unconfigured_provider_says_so() {
    let (base, _fake) = fake_server().await;
    let salon = Salon::new(&base).await;
    salon.app.calsync.configure(SyncConfig { google: Some(provider(&base)), microsoft: None, open_browser: false, demo: false });
    let (status, body) = salon.call(Method::POST, "/api/calendar-sync/connect", json!({"provider": "microsoft"})).await;
    assert_eq!(status, StatusCode::BAD_REQUEST);
    assert!(body["message"].as_str().unwrap().contains("Outlook bağlantısı ayarlanmamış"));

    salon
        .ok(Method::POST, "/api/users", json!({"name": "Mehmet", "username": "mehmet", "password": "Calisan2026", "role": "calisan", "isActive": true}))
        .await;
    let login = salon.ok(Method::POST, "/api/login", json!({"username": "mehmet", "password": "Calisan2026"})).await;
    let staff = Salon { app: salon.app.clone(), router: salon.router.clone(), clock: salon.clock.clone(), token: login["token"].as_str().unwrap().into() };
    let (status, _) = staff.call(Method::GET, "/api/calendar-sync", Value::Null).await;
    assert_eq!(status, StatusCode::FORBIDDEN);
    let (status, _) = staff.call(Method::POST, "/api/calendar-sync/connect", json!({"provider": "google"})).await;
    assert_eq!(status, StatusCode::FORBIDDEN);

    // Demo: kimlik olmasa da düğmeler görünür, basınca açıklama gelir.
    salon.app.calsync.configure(SyncConfig { demo: true, ..Default::default() });
    let st = salon.status().await;
    assert_eq!((st["googleAvailable"].clone(), st["microsoftAvailable"].clone(), st["demo"].clone()), (json!(true), json!(true), json!(true)));
    let (status, body) = salon.call(Method::POST, "/api/calendar-sync/connect", json!({"provider": "google"})).await;
    assert_eq!(status, StatusCode::BAD_REQUEST);
    assert!(body["message"].as_str().unwrap().starts_with("Bu bir demo"));
}
