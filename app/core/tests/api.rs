//! Uçtan uca senaryolar: gerçek ağ sunucusu (router) üzerinden, bellekteki veritabanıyla.

use std::sync::{Arc, Mutex};

use axum::body::Body;
use axum::http::{Method, Request, StatusCode};
use chrono::{Duration, NaiveDate, NaiveDateTime};
use http_body_util::BodyExt;
use salonera_core::{server, App};
use serde_json::{json, Value};
use tower::ServiceExt;

struct Api {
    router: axum::Router,
    clock: Arc<Mutex<NaiveDateTime>>,
    token: String,
}

fn start_time() -> NaiveDateTime {
    NaiveDate::from_ymd_opt(2026, 3, 2).unwrap().and_hms_opt(10, 0, 0).unwrap()
}

impl Api {
    async fn new() -> Api {
        let clock = Arc::new(Mutex::new(start_time()));
        let c = clock.clone();
        let app = App::in_memory(Arc::new(move || *c.lock().unwrap())).unwrap();
        let mut api = Api { router: server::router(app), clock, token: String::new() };
        let (status, _) = api
            .call(Method::POST, "/api/setup", json!({
                "businessName": "Kristal Düğün Salonu",
                "phone": "0212 555 00 00",
                "adminName": "Ayşe Yönetici",
                "adminUsername": "ayse",
                "adminPassword": "Salon2026",
                "halls": [{"name": "Kristal Salon", "capacity": 400}, {"name": "Bahçe", "capacity": 200}]
            }))
            .await;
        assert_eq!(status, StatusCode::OK);
        api.token = api.login("ayse", "Salon2026").await;
        api
    }

    fn set_today(&self, date: NaiveDate) {
        *self.clock.lock().unwrap() = date.and_hms_opt(10, 0, 0).unwrap();
    }

    async fn login(&self, username: &str, password: &str) -> String {
        let (status, body) =
            self.call(Method::POST, "/api/login", json!({"username": username, "password": password})).await;
        assert_eq!(status, StatusCode::OK, "giriş başarısız: {body}");
        body["token"].as_str().unwrap().to_string()
    }

    async fn call(&self, method: Method, path: &str, body: Value) -> (StatusCode, Value) {
        self.call_as(&self.token, method, path, body).await
    }

    async fn call_as(&self, token: &str, method: Method, path: &str, body: Value) -> (StatusCode, Value) {
        let mut request = Request::builder().method(method).uri(path).header("content-type", "application/json");
        if !token.is_empty() {
            request = request.header("authorization", format!("Bearer {token}"));
        }
        let request = request.body(if body.is_null() { Body::empty() } else { Body::from(body.to_string()) }).unwrap();
        let response = self.router.clone().oneshot(request).await.unwrap();
        let status = response.status();
        let bytes = response.into_body().collect().await.unwrap().to_bytes();
        let value = if bytes.is_empty() { Value::Null } else { serde_json::from_slice(&bytes).unwrap_or(Value::Null) };
        (status, value)
    }

    async fn get(&self, path: &str) -> Value {
        let (status, body) = self.call(Method::GET, path, Value::Null).await;
        assert_eq!(status, StatusCode::OK, "{path}: {body}");
        body
    }

    async fn ok(&self, method: Method, path: &str, body: Value) -> Value {
        let (status, response) = self.call(method, path, body).await;
        assert_eq!(status, StatusCode::OK, "{path}: {response}");
        response
    }

    async fn rule_error(&self, method: Method, path: &str, body: Value) -> String {
        let (status, response) = self.call(method, path, body).await;
        assert_eq!(status, StatusCode::BAD_REQUEST, "{path} hata bekleniyordu, gelen: {response}");
        response["message"].as_str().unwrap().to_string()
    }

    async fn hall(&self, index: usize) -> String {
        self.get("/api/halls").await[index]["id"].as_str().unwrap().to_string()
    }

    async fn package(&self, name: &str, price_tl: i64) -> String {
        self.ok(Method::POST, "/api/packages", json!({"name": name, "price": price_tl * 100, "isActive": true}))
            .await
            .as_str()
            .unwrap()
            .to_string()
    }

    async fn extra(&self, name: &str, price_tl: i64) -> String {
        self.ok(Method::POST, "/api/extras", json!({"name": name, "price": price_tl * 100, "isActive": true}))
            .await
            .as_str()
            .unwrap()
            .to_string()
    }
}

fn new_customer(name: &str) -> Value {
    json!({"fullName": name, "phone": "0532 111 22 33", "brideName": "Zeynep", "groomName": "Emre"})
}

fn option_request(hall: &str, date: &str, start: &str, end: &str, name: &str) -> Value {
    json!({
        "newCustomer": new_customer(name), "hallId": hall, "date": date, "start": start, "end": end,
        "kind": "dugun", "guestCount": 150, "optionDays": 7
    })
}

#[tokio::test]
async fn setup_only_once_and_login_rules() {
    let api = Api::new().await;
    let message = api
        .rule_error(Method::POST, "/api/setup", json!({
            "businessName": "X", "adminName": "Y", "adminUsername": "y", "adminPassword": "Parola123", "halls": [{"name": "A", "capacity": 10}]
        }))
        .await;
    assert!(message.contains("zaten"));

    // Yanlış parola → genel mesaj; 5 hatadan sonra kilit.
    for _ in 0..5 {
        let (status, body) = api.call_as("", Method::POST, "/api/login", json!({"username": "ayse", "password": "yanlis"})).await;
        assert_eq!(status, StatusCode::BAD_REQUEST);
        assert_eq!(body["message"], "Kullanıcı adı veya parola hatalı.");
    }
    let (_, body) = api.call_as("", Method::POST, "/api/login", json!({"username": "ayse", "password": "Salon2026"})).await;
    assert!(body["message"].as_str().unwrap().contains("dakika sonra"));

    // 5 dakika sonra doğru parola çalışır. Kullanıcı adı büyük/küçük harf duyarsız.
    *api.clock.lock().unwrap() += Duration::minutes(6);
    api.login("AYSE", "Salon2026").await;

    // Giriş yapmadan veri okunamaz.
    let (status, _) = api.call_as("", Method::GET, "/api/dashboard", Value::Null).await;
    assert_eq!(status, StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn hall_conflicts_follow_real_life_rules() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    let bahce = api.hall(1).await;

    api.ok(Method::POST, "/api/reservations", option_request(&kristal, "2026-06-13", "19:00", "23:00", "Ali Veli")).await;

    // Aynı salon, örtüşen saat → engellenir ve kimin tuttuğu söylenir.
    let message = api
        .rule_error(Method::POST, "/api/reservations", option_request(&kristal, "2026-06-13", "22:00", "01:00", "Can Kaya"))
        .await;
    assert!(message.contains("Kristal Salon bu saatlerde dolu"), "{message}");
    assert!(message.contains("Ali Veli"), "{message}");

    // Bitişik saat (23:00'te başlayan) serbest; başka salon serbest.
    api.ok(Method::POST, "/api/reservations", option_request(&kristal, "2026-06-13", "23:00", "02:00", "Gece Kına")).await;
    api.ok(Method::POST, "/api/reservations", option_request(&bahce, "2026-06-13", "19:00", "23:00", "Bahçe Müşterisi")).await;

    // Gece yarısını geçen kayıt ertesi günü de kapatır.
    let message = api
        .rule_error(Method::POST, "/api/reservations", option_request(&kristal, "2026-06-14", "01:00", "03:00", "Sabah"))
        .await;
    assert!(message.contains("dolu"));

    // Uygunluk sorgusu ekranda gösterilecek bilgiyi verir.
    let availability = api.get("/api/availability?date=2026-06-13&start=20:00&end=23:00").await;
    assert_eq!(availability[0]["isFree"], false);
    assert_eq!(availability[1]["isFree"], false);
    let availability = api.get("/api/availability?date=2026-06-20&start=20:00&end=23:00").await;
    assert_eq!(availability[0]["isFree"], true);
}

#[tokio::test]
async fn cancelled_and_expired_options_free_the_hall() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    let first = api.ok(Method::POST, "/api/reservations", option_request(&kristal, "2026-07-04", "19:00", "23:00", "Birinci")).await;
    let first_id = first["reservationId"].as_str().unwrap().to_string();

    // Ön rezervasyon süresi (7 gün) dolunca tarih başkasına verilebilir.
    api.set_today(NaiveDate::from_ymd_opt(2026, 3, 10).unwrap());
    let second = api.ok(Method::POST, "/api/reservations", option_request(&kristal, "2026-07-04", "19:00", "23:00", "İkinci")).await;

    // Süresi dolan ilk kayıt artık uzatılamaz ve kesinleştirilemez.
    let message = api.rule_error(Method::POST, &format!("/api/reservations/{first_id}/extend"), json!({"days": 7})).await;
    assert!(message.contains("başkasına verilmiş"), "{message}");

    // İkinci kayıt iptal edilince tarih tekrar boşalır.
    let second_id = second["reservationId"].as_str().unwrap();
    let message = api.rule_error(Method::POST, &format!("/api/reservations/{second_id}/cancel"), json!({"reason": " "})).await;
    assert!(message.contains("İptal nedeni"));
    api.ok(Method::POST, &format!("/api/reservations/{second_id}/cancel"), json!({"reason": "Müşteri vazgeçti"})).await;
    api.ok(Method::POST, &format!("/api/reservations/{first_id}/extend"), json!({"days": 5})).await;
    let detail = api.get(&format!("/api/reservations/{first_id}")).await;
    assert_eq!(detail["summary"]["optionUntil"], "2026-03-15");
    assert_eq!(detail["summary"]["optionExpired"], false);
}

#[tokio::test]
async fn confirmed_sale_with_deposit_plan_and_payments() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    let gold = api.package("Altın Paket", 200_000).await;
    let photo = api.extra("Fotoğraf", 20_000).await;

    let created = api
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": new_customer("İpek Işık"), "hallId": kristal, "date": "2026-09-12",
            "start": "19:00", "end": "00:00", "kind": "dugun", "guestCount": 350,
            "sale": {
                "packageId": gold, "extraIds": [photo], "discount": 10_000_00,
                "firstPayment": {"amount": 50_000_00, "method": "nakit"}
            }
        }))
        .await;
    assert_eq!(created["no"], "2026-0001");
    assert!(created["paymentId"].is_string());
    let id = created["reservationId"].as_str().unwrap().to_string();

    // 200.000 + 20.000 − 10.000 = 210.000 TL. Plan otomatik: kapora + düğünden 1 hafta önce kalan.
    let detail = api.get(&format!("/api/reservations/{id}")).await;
    let summary = &detail["summary"];
    assert_eq!(summary["status"], "kesin");
    assert_eq!(summary["total"], 210_000_00);
    assert_eq!(summary["paid"], 50_000_00);
    assert_eq!(summary["remaining"], 160_000_00);
    assert_eq!(detail["packagePrice"], 200_000_00);
    assert_eq!(detail["extras"][0]["name"], "Fotoğraf");
    let plan = detail["installments"].as_array().unwrap();
    assert_eq!(plan.len(), 2);
    assert_eq!(plan[0]["label"], "Kapora");
    assert_eq!(plan[0]["state"], "odendi");
    assert_eq!(plan[1]["dueDate"], "2026-09-05");
    assert_eq!(plan[1]["state"], "bekliyor");

    // Paket fiyatı sonradan değişse de bu sözleşme değişmez.
    api.ok(Method::PUT, &format!("/api/packages/{gold}"), json!({"name": "Altın Paket", "price": 999_00, "isActive": true})).await;
    assert_eq!(api.get(&format!("/api/reservations/{id}")).await["summary"]["total"], 210_000_00);

    // Plan, taksitlere bölünür; toplam tutmazsa reddedilir.
    let message = api
        .rule_error(Method::PUT, &format!("/api/reservations/{id}/plan"), json!({"plan": [
            {"label": "Kapora", "dueDate": "2026-03-02", "amount": 50_000_00},
            {"label": "1. taksit", "dueDate": "2026-05-01", "amount": 10_00}
        ]}))
        .await;
    assert!(message.contains("eşit olmalıdır"), "{message}");
    api.ok(Method::PUT, &format!("/api/reservations/{id}/plan"), json!({"plan": [
        {"label": "Kapora", "dueDate": "2026-03-02", "amount": 50_000_00},
        {"label": "1. taksit", "dueDate": "2026-05-01", "amount": 80_000_00},
        {"label": "2. taksit", "dueDate": "2026-08-01", "amount": 80_000_00}
    ]})).await;

    // Kısmi ödeme ilk açık taksite düşer. Fazla ödeme reddedilir.
    api.ok(Method::POST, &format!("/api/reservations/{id}/payments"), json!({"amount": 30_000_00, "method": "kredi_karti"})).await;
    let message = api
        .rule_error(Method::POST, &format!("/api/reservations/{id}/payments"), json!({"amount": 130_000_01, "method": "nakit"}))
        .await;
    assert!(message.contains("kalan borçtan (130.000,00 TL)"), "{message}");

    // Tarih geçince taksit "gecikti" olur ve ana sayfada görünür.
    api.set_today(NaiveDate::from_ymd_opt(2026, 5, 10).unwrap());
    let detail = api.get(&format!("/api/reservations/{id}")).await;
    assert_eq!(detail["installments"][1]["paid"], 30_000_00);
    assert_eq!(detail["installments"][1]["state"], "gecikti");
    assert_eq!(detail["summary"]["overdue"], 50_000_00);
    let dashboard = api.get("/api/dashboard").await;
    assert_eq!(dashboard["overdueTotal"], 50_000_00);
    assert_eq!(dashboard["overdue"][0]["customerName"], "İpek Işık");

    // İade: ödenenden fazla olamaz; neden zorunlu.
    let message = api
        .rule_error(Method::POST, &format!("/api/reservations/{id}/refunds"), json!({"amount": 90_000_00, "method": "nakit", "reason": "x"}))
        .await;
    assert!(message.contains("ödediği tutardan"), "{message}");
    api.ok(Method::POST, &format!("/api/reservations/{id}/refunds"), json!({"amount": 5_000_00, "method": "havale", "reason": "Fazla ödeme"})).await;
    let summary = &api.get(&format!("/api/reservations/{id}")).await["summary"];
    assert_eq!(summary["paid"], 75_000_00);
    assert_eq!(summary["remaining"], 135_000_00);
}

#[tokio::test]
async fn option_becomes_confirmed_and_rules_hold() {
    let api = Api::new().await;
    let bahce = api.hall(1).await;
    let package = api.package("Kokteyl", 50_000).await;

    // Kapasite aşılırsa açıkça söylenir.
    let mut too_many = option_request(&bahce, "2026-05-20", "15:00", "18:00", "Kalabalık");
    too_many["guestCount"] = json!(500);
    let message = api.rule_error(Method::POST, "/api/reservations", too_many).await;
    assert!(message.contains("en fazla 200 kişi"), "{message}");

    // Geçmiş tarih olmaz.
    let message =
        api.rule_error(Method::POST, "/api/reservations", option_request(&bahce, "2026-01-10", "15:00", "18:00", "Eski")).await;
    assert!(message.contains("Geçmiş"));

    let created = api.ok(Method::POST, "/api/reservations", option_request(&bahce, "2026-05-20", "15:00", "18:00", "Nişan Ailesi")).await;
    let id = created["reservationId"].as_str().unwrap().to_string();

    // Ön rezervasyona ödeme alınamaz.
    let message = api
        .rule_error(Method::POST, &format!("/api/reservations/{id}/payments"), json!({"amount": 100_00, "method": "nakit"}))
        .await;
    assert!(message.contains("Önce kaydı kesinleştirin"));

    // İndirim fiyatı aşamaz.
    let message = api
        .rule_error(Method::POST, &format!("/api/reservations/{id}/confirm"), json!({"packageId": package, "discount": 60_000_00}))
        .await;
    assert!(message.contains("İndirim"));

    api.ok(Method::POST, &format!("/api/reservations/{id}/confirm"), json!({
        "packageId": package, "firstPayment": {"amount": 10_000_00, "method": "havale"}
    })).await;
    let detail = api.get(&format!("/api/reservations/{id}")).await;
    assert_eq!(detail["summary"]["status"], "kesin");
    assert_eq!(detail["summary"]["optionUntil"], Value::Null);
    assert_eq!(detail["summary"]["remaining"], 40_000_00);

    // İkinci kez kesinleştirilemez.
    let message = api.rule_error(Method::POST, &format!("/api/reservations/{id}/confirm"), json!({"packageId": package})).await;
    assert!(message.contains("Yalnızca ön rezervasyonlar"));

    // Tarih/salon değişikliği aynı çakışma kurallarına tabidir.
    api.ok(Method::PUT, &format!("/api/reservations/{id}"), json!({
        "hallId": bahce, "date": "2026-05-21", "start": "16:00", "end": "19:00", "kind": "nisan", "guestCount": 180, "notes": "Pasta çikolatalı"
    })).await;
    let summary = &api.get(&format!("/api/reservations/{id}")).await["summary"];
    assert_eq!(summary["date"], "2026-05-21");
    assert_eq!(summary["start"], "16:00");
}

#[tokio::test]
async fn staff_permissions_and_payment_cancellation() {
    let api = Api::new().await;
    api.ok(Method::POST, "/api/users", json!({"name": "Mehmet Çalışan", "username": "mehmet", "password": "Calisan1", "role": "calisan", "isActive": true})).await;
    let staff = api.login("mehmet", "Calisan1").await;

    // Çalışan ayarları, raporları ve kullanıcıları göremez/değiştiremez.
    for (method, path, body) in [
        (Method::GET, "/api/users", Value::Null),
        (Method::GET, "/api/reports/year", Value::Null),
        (Method::POST, "/api/packages", json!({"name": "X", "price": 1, "isActive": true})),
    ] {
        let (status, _) = api.call_as(&staff, method, path, body).await;
        assert_eq!(status, StatusCode::FORBIDDEN, "{path}");
    }

    // Ama rezervasyon yapıp ödeme alabilir.
    let hall = api.hall(0).await;
    let package = api.package("Gümüş", 100_000).await;
    let (status, created) = api
        .call_as(&staff, Method::POST, "/api/reservations", json!({
            "newCustomer": new_customer("Selin Arslan"), "hallId": hall, "date": "2026-10-10", "start": "20:00", "end": "23:30",
            "kind": "dugun", "guestCount": 300, "sale": {"packageId": package, "firstPayment": {"amount": 20_000_00, "method": "nakit"}}
        }))
        .await;
    assert_eq!(status, StatusCode::OK, "{created}");
    let id = created["reservationId"].as_str().unwrap();
    let payment_id = created["paymentId"].as_str().unwrap();

    // Hatalı ödemeyi yalnızca yönetici iptal eder; iptal edilen ödeme borca geri eklenir.
    let (status, _) = api.call_as(&staff, Method::POST, &format!("/api/payments/{payment_id}/cancel"), json!({"reason": "Yanlış"})).await;
    assert_eq!(status, StatusCode::FORBIDDEN);
    api.ok(Method::POST, &format!("/api/payments/{payment_id}/cancel"), json!({"reason": "Yanlış tutar girildi"})).await;
    let summary = &api.get(&format!("/api/reservations/{id}")).await["summary"];
    assert_eq!(summary["paid"], 0);
    assert_eq!(summary["remaining"], 100_000_00);

    // Son yönetici yetkisizleştirilemez.
    let users = api.get("/api/users").await;
    let admin = users.as_array().unwrap().iter().find(|u| u["role"] == "yonetici").unwrap();
    let message = api
        .rule_error(Method::PUT, &format!("/api/users/{}", admin["id"].as_str().unwrap()), json!({
            "name": "Ayşe", "username": "ayse", "role": "calisan", "isActive": true
        }))
        .await;
    assert!(message.contains("En az bir aktif yönetici"));

    // Pasif yapılan çalışanın oturumu hemen kapanır.
    let mehmet = users.as_array().unwrap().iter().find(|u| u["username"] == "mehmet").unwrap();
    api.ok(Method::PUT, &format!("/api/users/{}", mehmet["id"].as_str().unwrap()), json!({
        "name": "Mehmet Çalışan", "username": "mehmet", "role": "calisan", "isActive": false
    })).await;
    let (status, _) = api.call_as(&staff, Method::GET, "/api/dashboard", Value::Null).await;
    assert_eq!(status, StatusCode::UNAUTHORIZED);
}

#[tokio::test]
async fn customer_search_and_reports() {
    let api = Api::new().await;
    let hall = api.hall(0).await;
    let package = api.package("Standart", 120_000).await;
    let created = api
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": {"fullName": "IŞIL ÖZTÜRK", "phone": "0 (533) 444-55-66", "groomName": "Kerem"},
            "hallId": hall, "date": "2026-04-18", "start": "19:00", "end": "23:00", "kind": "dugun", "guestCount": 200,
            "sale": {"packageId": package, "firstPayment": {"amount": 30_000_00, "method": "nakit"}}
        }))
        .await;
    let id = created["reservationId"].as_str().unwrap().to_string();

    // Türkçe harfler, damat adı ve telefonun bir kısmıyla bulunur.
    for query in ["ışıl", "IŞIL", "kerem", "4445566", "533 444"] {
        let found = api.get(&format!("/api/customers?q={}", urlencode(query))).await;
        assert_eq!(found.as_array().unwrap().len(), 1, "arama: {query}");
    }
    let found = api.get(&format!("/api/reservations?q={}", urlencode("öztürk"))).await;
    assert_eq!(found[0]["id"], id.as_str());

    // Aktif kaydı olan müşteri silinemez.
    let customer_id = found[0]["customerId"].as_str().unwrap();
    let message = api.rule_error(Method::DELETE, &format!("/api/customers/{customer_id}"), Value::Null).await;
    assert!(message.contains("ileri tarihli"));

    // Yıllık rapor: satış etkinlik ayına, tahsilat ödeme ayına yazılır.
    let report = api.get("/api/reports/year?year=2026").await;
    assert_eq!(report["months"][3]["sales"], 120_000_00);
    assert_eq!(report["months"][3]["eventCount"], 1);
    assert_eq!(report["months"][2]["collected"], 30_000_00);
    assert_eq!(report["totalSales"], 120_000_00);

    let receivables = api.get("/api/reports/receivables").await;
    assert_eq!(receivables[0]["remaining"], 90_000_00);

    let payments = api.get("/api/payments?from=2026-03-01&to=2026-03-31").await;
    assert_eq!(payments.as_array().unwrap().len(), 1);
    assert_eq!(payments[0]["no"], "M-00001");
    assert_eq!(payments[0]["customerName"], "IŞIL ÖZTÜRK");

    // Takvim aralığı.
    let calendar = api.get("/api/calendar?from=2026-04-01&to=2026-05-01").await;
    assert_eq!(calendar.as_array().unwrap().len(), 1);
}

fn urlencode(text: &str) -> String {
    text.bytes()
        .map(|b| match b {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'.' | b'_' => (b as char).to_string(),
            _ => format!("%{b:02X}"),
        })
        .collect()
}
