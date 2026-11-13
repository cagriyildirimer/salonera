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
async fn package_price_follows_guest_count() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    // 50.000 TL sabit + kişi başı 1.000 TL, en az 200 kişi üzerinden.
    let menu = api
        .ok(Method::POST, "/api/packages", json!({
            "name": "Menü Paketi", "price": 50_000_00, "perGuestPrice": 1_000_00, "minGuests": 200, "isActive": true
        }))
        .await
        .as_str()
        .unwrap()
        .to_string();
    let listed = api.get("/api/packages").await;
    assert_eq!(listed[0]["perGuestPrice"], 1_000_00);
    assert_eq!(listed[0]["minGuests"], 200);

    // 150 davetli, en az 200 kişi sınırına takılır: 50.000 + 200 × 1.000 = 250.000 TL.
    let created = api
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": new_customer("Selin Kaya"), "hallId": kristal, "date": "2026-09-12",
            "start": "19:00", "end": "00:00", "kind": "dugun", "guestCount": 150,
            "sale": {"packageId": menu, "firstPayment": {"amount": 50_000_00, "method": "nakit"}}
        }))
        .await;
    let id = created["reservationId"].as_str().unwrap().to_string();
    let path = format!("/api/reservations/{id}");
    let detail = api.get(&path).await;
    assert_eq!(detail["packagePrice"], 250_000_00);
    assert_eq!(detail["summary"]["total"], 250_000_00);
    assert_eq!(detail["packagePricing"]["perGuestPrice"], 1_000_00);

    let edit = |guests: i64, reprice: bool| json!({
        "hallId": kristal, "date": "2026-09-12", "start": "19:00", "end": "00:00",
        "kind": "dugun", "guestCount": guests, "reprice": reprice
    });

    // Paket fiyatı sonradan artsa da yeniden hesaplama satış anındaki fiyatlarla yapılır.
    api.ok(Method::PUT, &format!("/api/packages/{menu}"), json!({
        "name": "Menü Paketi", "price": 90_000_00, "perGuestPrice": 2_000_00, "minGuests": 0, "isActive": true
    })).await;

    // "Fiyatı güncelleme" denirse sözleşme olduğu gibi kalır.
    api.ok(Method::PUT, &path, edit(300, false)).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["summary"]["guestCount"], 300);
    assert_eq!(detail["summary"]["total"], 250_000_00);

    // Güncellenirse: 50.000 + 350 × 1.000 = 400.000 TL; fark ödenmemiş son taksite eklenir.
    api.ok(Method::PUT, &path, edit(350, true)).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["packagePrice"], 400_000_00);
    assert_eq!(detail["summary"]["total"], 400_000_00);
    let plan = detail["installments"].as_array().unwrap();
    assert_eq!(plan.len(), 2);
    assert_eq!(plan[0]["amount"], 50_000_00);
    assert_eq!(plan[0]["state"], "odendi");
    assert_eq!(plan[1]["amount"], 350_000_00);

    // Azalınca en az kişi sınırı yine geçerli; fark ödenmemiş taksitten düşülür.
    api.ok(Method::PUT, &path, edit(100, true)).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["summary"]["total"], 250_000_00);
    assert_eq!(detail["installments"][1]["amount"], 200_000_00);

    // Hepsi ödenmişken davetli artarsa yeni bir "Ek ödeme" taksiti açılır.
    api.ok(Method::POST, &format!("{path}/payments"), json!({"amount": 200_000_00, "method": "havale"})).await;
    api.ok(Method::PUT, &path, edit(250, true)).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["summary"]["total"], 300_000_00);
    assert_eq!(detail["summary"]["remaining"], 50_000_00);
    let plan = detail["installments"].as_array().unwrap();
    assert_eq!(plan.len(), 3);
    assert_eq!(plan[2]["label"], "Ek ödeme (davetli artışı)");
    assert_eq!(plan[2]["amount"], 50_000_00);
    assert_eq!(plan[2]["dueDate"], "2026-09-05");

    // Yeni tutar ödenmiş paradan az olamaz; kişi sayısı da değişmeden kalır.
    api.ok(Method::POST, &format!("{path}/payments"), json!({"amount": 50_000_00, "method": "nakit"})).await;
    let message = api.rule_error(Method::PUT, &path, edit(150, true)).await;
    assert!(message.contains("Önce iade yapın"), "{message}");
    assert_eq!(api.get(&path).await["summary"]["guestCount"], 250);
}

#[tokio::test]
async fn food_menus_are_priced_copied_and_editable() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    let dishes = |main: &str| json!([
        {"course": "Çorba", "name": "Mercimek çorbası"},
        {"course": "Ana yemek", "name": main},
        {"course": "Tatlı", "name": "Fırın sütlaç"}
    ]);

    // Yemeksiz menü kaydedilemez; kişi başı fark eksi olamaz.
    let message = api.rule_error(Method::POST, "/api/menus", json!({"name": "Boş", "isActive": true, "items": []})).await;
    assert!(message.contains("en az bir yemek"), "{message}");
    let message = api
        .rule_error(Method::POST, "/api/menus", json!({"name": "X", "perGuestPrice": -1, "isActive": true, "items": dishes("Pilav")}))
        .await;
    assert!(message.contains("eksi"), "{message}");

    let classic = api
        .ok(Method::POST, "/api/menus", json!({"name": "Klasik Menü", "isActive": true, "items": dishes("Tavuk sote")}))
        .await
        .as_str()
        .unwrap()
        .to_string();
    let lamb = api
        .ok(Method::POST, "/api/menus", json!({
            "name": "Kuzu Tandır Menüsü", "perGuestPrice": 150_00, "isActive": true, "items": dishes("Kuzu tandır")
        }))
        .await
        .as_str()
        .unwrap()
        .to_string();
    let listed = api.get("/api/menus").await;
    assert_eq!(listed.as_array().unwrap().len(), 2);
    assert_eq!(listed[0]["name"], "Klasik Menü"); // farksız menü önce
    assert_eq!(listed[1]["items"][1]["name"], "Kuzu tandır");

    // 50.000 TL sabit + kişi başı 1.000 TL (en az 200 kişi). Menü farkı da en az 200 kişi üzerinden:
    // 50.000 + 200 × 1.000 + 200 × 150 = 280.000 TL.
    let package = api
        .ok(Method::POST, "/api/packages", json!({
            "name": "Yemekli Paket", "price": 50_000_00, "perGuestPrice": 1_000_00, "minGuests": 200, "isActive": true
        }))
        .await
        .as_str()
        .unwrap()
        .to_string();
    let created = api
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": new_customer("Derya Aydın"), "hallId": kristal, "date": "2026-10-10",
            "start": "19:00", "end": "23:30", "kind": "dugun", "guestCount": 150,
            "sale": {"packageId": package, "menuId": lamb, "firstPayment": {"amount": 50_000_00, "method": "nakit"}}
        }))
        .await;
    let id = created["reservationId"].as_str().unwrap().to_string();
    let path = format!("/api/reservations/{id}");
    let detail = api.get(&path).await;
    assert_eq!(detail["menuName"], "Kuzu Tandır Menüsü");
    assert_eq!(detail["menuPerGuest"], 150_00);
    assert_eq!(detail["menuPrice"], 30_000_00);
    assert_eq!(detail["summary"]["total"], 280_000_00);
    assert_eq!(detail["menuItems"], dishes("Kuzu tandır"));

    // Katalogdaki menü değişse de yapılmış sözleşme ve o düğünün menüsü değişmez.
    api.ok(Method::PUT, &format!("/api/menus/{lamb}"), json!({
        "name": "Kuzu Tandır Menüsü", "perGuestPrice": 400_00, "isActive": true, "items": dishes("Kuzu incik")
    })).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["menuPrice"], 30_000_00);
    assert_eq!(detail["menuItems"][1]["name"], "Kuzu tandır");

    // Düğüne özel düzenleme: yemekler ve mutfak notu değişir, fiyat değişmez.
    api.ok(Method::PUT, &format!("{path}/menu"), json!({
        "items": [{"course": "Ana yemek", "name": "Kuzu tandır"}, {"course": "Tatlı", "name": "Künefe"}],
        "notes": "12 çocuk menüsü, 3 vejetaryen"
    })).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["menuItems"].as_array().unwrap().len(), 2);
    assert_eq!(detail["menuItems"][1]["name"], "Künefe");
    assert_eq!(detail["menuNotes"], "12 çocuk menüsü, 3 vejetaryen");
    assert_eq!(detail["summary"]["total"], 280_000_00);
    let message = api.rule_error(Method::PUT, &format!("{path}/menu"), json!({"items": [{"course": "", "name": "Pilav"}]})).await;
    assert!(message.contains("bölümü"), "{message}");

    // Davetli artınca menü farkı da satış anındaki fiyatla yeniden hesaplanır: 50.000 + 300 × 1.150 = 395.000 TL.
    api.ok(Method::PUT, &path, json!({
        "hallId": kristal, "date": "2026-10-10", "start": "19:00", "end": "23:30",
        "kind": "dugun", "guestCount": 300, "reprice": true
    })).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["packagePrice"], 350_000_00);
    assert_eq!(detail["menuPrice"], 45_000_00);
    assert_eq!(detail["summary"]["total"], 395_000_00);

    // Farksız menüye geçilince fark ödenmemiş taksitten düşülür; yemekler yeni menüden gelir, mutfak notu kalır.
    api.ok(Method::POST, &format!("{path}/menu/select"), json!({"menuId": classic})).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["menuName"], "Klasik Menü");
    assert_eq!(detail["menuPrice"], 0);
    assert_eq!(detail["summary"]["total"], 350_000_00);
    assert_eq!(detail["installments"][1]["amount"], 300_000_00);
    assert_eq!(detail["menuItems"][1]["name"], "Tavuk sote");
    assert_eq!(detail["menuNotes"], "12 çocuk menüsü, 3 vejetaryen");

    // Hepsi ödenmişken farklı menüye geçilirse "menü değişikliği" taksiti açılır (yeni fiyat: 300 × 400 TL).
    api.ok(Method::POST, &format!("{path}/payments"), json!({"amount": 300_000_00, "method": "havale"})).await;
    api.ok(Method::POST, &format!("{path}/menu/select"), json!({"menuId": lamb})).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["menuPrice"], 120_000_00);
    assert_eq!(detail["summary"]["remaining"], 120_000_00);
    let plan = detail["installments"].as_array().unwrap();
    assert_eq!(plan.last().unwrap()["label"], "Ek ödeme (menü değişikliği)");

    // Ödenmiş paranın altına inen menü değişikliği yapılamaz.
    api.ok(Method::POST, &format!("{path}/payments"), json!({"amount": 120_000_00, "method": "havale"})).await;
    let message = api.rule_error(Method::POST, &format!("{path}/menu/select"), json!({"menuId": null})).await;
    assert!(message.contains("menüyü değiştirin"), "{message}");
    assert_eq!(api.get(&path).await["menuName"], "Kuzu Tandır Menüsü");

    // Ön rezervasyonda menü seçilmez; kesin kayda çevrilirken seçilir. Kullanımdan kaldırılan menü satılamaz.
    let option = api.ok(Method::POST, "/api/reservations", option_request(&kristal, "2026-11-14", "19:00", "23:00", "Can Er")).await;
    let option_id = option["reservationId"].as_str().unwrap().to_string();
    let message = api.rule_error(Method::POST, &format!("/api/reservations/{option_id}/menu/select"), json!({"menuId": classic})).await;
    assert!(message.contains("kesin kayıtta"), "{message}");
    api.ok(Method::PUT, &format!("/api/menus/{classic}"), json!({
        "name": "Klasik Menü", "isActive": false, "items": dishes("Tavuk sote")
    })).await;
    assert_eq!(api.get("/api/menus").await.as_array().unwrap().len(), 1);
    assert_eq!(api.get("/api/menus?all=true").await.as_array().unwrap().len(), 2);
    let message = api
        .rule_error(Method::POST, &format!("/api/reservations/{option_id}/confirm"), json!({"packageId": package, "menuId": classic}))
        .await;
    assert!(message.contains("menü bulunamadı"), "{message}");
}

#[tokio::test]
async fn option_groups_like_cake_flavours() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    let extra = |body: Value| async { api.ok(Method::POST, "/api/extras", body).await.as_str().unwrap().to_string() };
    let banana = extra(json!({"name": "Muzlu pasta", "groupName": "Pasta", "price": 3_500_00, "isActive": true})).await;
    let strawberry = extra(json!({"name": "Çilekli pasta", "groupName": "Pasta", "price": 4_000_00, "isActive": true})).await;
    // Kişi başı seçenek: 1.000 TL sabit + kişi başı 20 TL.
    let nuts = extra(json!({"name": "Kuruyemiş ikramı", "price": 1_000_00, "perGuestPrice": 20_00, "isActive": true})).await;
    let listed = api.get("/api/extras").await;
    assert_eq!(listed[0]["groupName"], Value::Null); // grupsuzlar önce, sonra grup grup
    assert_eq!(listed[1]["groupName"], "Pasta");
    assert_eq!(listed[1]["name"], "Muzlu pasta");

    let package = api.package("Salon Paketi", 100_000).await;
    let sale = |ids: Value| json!({
        "newCustomer": new_customer("Gizem Ak"), "hallId": kristal, "date": "2026-08-22",
        "start": "19:00", "end": "23:00", "kind": "dugun", "guestCount": 250,
        "sale": {"packageId": package, "extraIds": ids, "firstPayment": {"amount": 20_000_00, "method": "nakit"}}
    });

    // Aynı gruptan iki seçenek seçilemez.
    let message = api.rule_error(Method::POST, "/api/reservations", sale(json!([banana, strawberry]))).await;
    assert!(message.contains("“Pasta” seçeneklerinden yalnızca biri"), "{message}");

    // 100.000 + muzlu 3.500 + kuruyemiş (1.000 + 250 × 20 = 6.000) = 109.500 TL.
    let created = api.ok(Method::POST, "/api/reservations", sale(json!([banana, nuts]))).await;
    let id = created["reservationId"].as_str().unwrap().to_string();
    let path = format!("/api/reservations/{id}");
    let detail = api.get(&path).await;
    assert_eq!(detail["summary"]["total"], 109_500_00);
    let extras = detail["extras"].as_array().unwrap();
    assert_eq!(extras.len(), 2);
    assert_eq!(extras[0]["groupName"], "Pasta");
    assert_eq!(extras[0]["extraId"], banana.as_str());
    assert_eq!(extras[1]["price"], 6_000_00);
    assert_eq!(extras[1]["perGuest"], 20_00);

    // Davetli artınca kişi başı seçenek yeniden hesaplanır: 1.000 + 300 × 20 = 7.000 TL.
    api.ok(Method::PUT, &path, json!({
        "hallId": kristal, "date": "2026-08-22", "start": "19:00", "end": "23:00",
        "kind": "dugun", "guestCount": 300, "reprice": true
    })).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["summary"]["total"], 110_500_00);
    assert_eq!(detail["extras"][1]["price"], 7_000_00);

    // Katalog fiyatı artsa da rezervasyondaki seçenek eski fiyatla kalır; yeni seçilen güncel fiyatla gelir.
    api.ok(Method::PUT, &format!("/api/extras/{nuts}"), json!({
        "name": "Kuruyemiş ikramı", "price": 9_000_00, "perGuestPrice": 50_00, "isActive": true
    })).await;
    // Tadımdan sonra pasta çilekliye döndü: fark (+500 TL) ödenmemiş taksite eklenir.
    api.ok(Method::PUT, &format!("{path}/extras"), json!({"extraIds": [strawberry, nuts]})).await;
    let detail = api.get(&path).await;
    assert_eq!(detail["summary"]["total"], 111_000_00);
    assert_eq!(detail["extras"][0]["name"], "Çilekli pasta");
    assert_eq!(detail["extras"][1]["price"], 7_000_00);
    let plan = detail["installments"].as_array().unwrap();
    assert_eq!(plan.iter().map(|x| x["amount"].as_i64().unwrap()).sum::<i64>(), 111_000_00);

    // Pasta istemiyorsa kaldırılır; gruptan iki seçenek yine olmaz.
    let message = api.rule_error(Method::PUT, &format!("{path}/extras"), json!({"extraIds": [strawberry, banana]})).await;
    assert!(message.contains("yalnızca biri"), "{message}");
    api.ok(Method::PUT, &format!("{path}/extras"), json!({"extraIds": [nuts]})).await;
    assert_eq!(api.get(&path).await["summary"]["total"], 107_000_00);

    // Ön rezervasyonda seçenek değiştirilemez (kesin kayıtta seçilir).
    let option = api.ok(Method::POST, "/api/reservations", option_request(&kristal, "2026-11-21", "19:00", "23:00", "Can Er")).await;
    let message = api
        .rule_error(Method::PUT, &format!("/api/reservations/{}/extras", option["reservationId"].as_str().unwrap()), json!({"extraIds": [banana]}))
        .await;
    assert!(message.contains("kesin kayıtta"), "{message}");
}

#[tokio::test]
async fn event_program_timeline_and_music() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    let created = api
        .ok(Method::POST, "/api/reservations", option_request(&kristal, "2026-09-12", "19:00", "01:00", "Ece Demir"))
        .await;
    let id = created["reservationId"].as_str().unwrap().to_string();
    let path = format!("/api/reservations/{id}/program");

    // Boş plan.
    let empty = api.get(&path).await;
    assert_eq!(empty["items"].as_array().unwrap().len(), 0);
    assert_eq!(empty["music"].as_array().unwrap().len(), 0);

    // Akış saate göre sıralanır; gece yarısını geçen 00:30 en sona gelir. "www." bağlantısına https:// eklenir.
    let saved = api
        .ok(Method::PUT, &path, json!({
            "items": [
                {"time": "21:30", "title": "Takı töreni"},
                {"time": "00:30", "title": "Kapanış"},
                {"time": "20:00", "title": "Yemek servisi", "note": "Ana yemek 20:30"},
                {"time": "21:00", "title": "Pasta kesimi"},
                {"time": "18:30", "title": "Gelin hazırlık"}
            ],
            "music": [
                {"label": "Giriş müziği", "song": "Can't Help Falling in Love", "link": "https://youtu.be/vGJTaP6anOU"},
                {"label": "İlk dans", "link": "www.youtube.com/watch?v=abc"}
            ],
            "notes": "Masalar beyaz örtü"
        }))
        .await;
    let times: Vec<&str> = saved["items"].as_array().unwrap().iter().map(|x| x["time"].as_str().unwrap()).collect();
    assert_eq!(times, ["18:30", "20:00", "21:00", "21:30", "00:30"]);
    assert_eq!(saved["items"][1]["note"], "Ana yemek 20:30");
    assert_eq!(saved["music"][1]["link"], "https://www.youtube.com/watch?v=abc");
    assert_eq!(api.get(&path).await["notes"], "Masalar beyaz örtü");

    // Hatalı girişler.
    let message = api.rule_error(Method::PUT, &path, json!({"items": [{"time": "25:00", "title": "Pasta"}]})).await;
    assert!(message.contains("saati geçersiz"), "{message}");
    let message = api.rule_error(Method::PUT, &path, json!({"music": [{"label": "Çıkış", "link": "javascript:alert(1)"}]})).await;
    assert!(message.contains("bağlantısı geçersiz"), "{message}");
    let message = api.rule_error(Method::PUT, &path, json!({"music": [{"label": "Çıkış"}]})).await;
    assert!(message.contains("şarkı adını"), "{message}");
    // Hatalı kayıt planı bozmaz.
    assert_eq!(api.get(&path).await["items"].as_array().unwrap().len(), 5);

    // İptal edilen kaydın planı değiştirilemez.
    api.ok(Method::POST, &format!("/api/reservations/{id}/cancel"), json!({"reason": "Vazgeçtiler"})).await;
    let message = api.rule_error(Method::PUT, &path, json!({"items": []})).await;
    assert!(message.contains("İptal edilmiş"), "{message}");
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

#[tokio::test]
async fn reports_list_sales_details_and_open_installments() {
    let api = Api::new().await;
    let kristal = api.hall(0).await;
    let bahce = api.hall(1).await;
    let gold = api.package("Altın Paket", 200_000).await;
    let photo = api.extra("Fotoğraf", 20_000).await;

    let sold = api
        .ok(Method::POST, "/api/reservations", json!({
            "newCustomer": new_customer("İpek Işık"), "hallId": kristal, "date": "2026-09-12",
            "start": "19:00", "end": "00:00", "kind": "dugun", "guestCount": 350,
            "sale": {"packageId": gold, "extraIds": [photo], "discount": 10_000_00,
                     "firstPayment": {"amount": 50_000_00, "method": "nakit"}}
        }))
        .await["reservationId"].as_str().unwrap().to_string();
    api.ok(Method::PUT, &format!("/api/reservations/{sold}/plan"), json!({"plan": [
        {"label": "Kapora", "dueDate": "2026-03-02", "amount": 50_000_00},
        {"label": "1. taksit", "dueDate": "2026-05-01", "amount": 80_000_00},
        {"label": "2. taksit", "dueDate": "2026-08-01", "amount": 80_000_00}
    ]})).await;
    api.ok(Method::POST, &format!("/api/reservations/{sold}/payments"), json!({"amount": 30_000_00, "method": "havale"})).await;
    let cancelled = api
        .ok(Method::POST, "/api/reservations", option_request(&bahce, "2026-09-13", "14:00", "18:00", "Ece Kaya"))
        .await["reservationId"].as_str().unwrap().to_string();
    api.ok(Method::POST, &format!("/api/reservations/{cancelled}/cancel"), json!({"reason": "Başka salon"})).await;
    // Aralık dışındaki kayıt rapora girmez.
    api.ok(Method::POST, "/api/reservations", option_request(&bahce, "2026-11-01", "14:00", "18:00", "Ayrı Gün")).await;

    // Satış analizi: iptaller dahil, paket ve ek hizmet ayrıntısıyla, kaydı açan kişiyle.
    let sales = api.get("/api/reports/sales?from=2026-09-01&to=2026-09-30").await;
    let rows = sales.as_array().unwrap();
    assert_eq!(rows.len(), 2);
    assert_eq!(rows[0]["customerName"], "İpek Işık");
    assert_eq!(rows[0]["packageName"], "Altın Paket");
    assert_eq!(rows[0]["extras"][0]["name"], "Fotoğraf");
    assert_eq!(rows[0]["discount"], 10_000_00);
    assert_eq!(rows[0]["total"], 210_000_00);
    assert_eq!(rows[0]["paid"], 80_000_00);
    assert_eq!(rows[0]["createdBy"], "Ayşe Yönetici");
    assert_eq!(rows[0]["createdOn"], "2026-03-02");
    assert_eq!(rows[1]["status"], "iptal");
    assert_eq!(rows[1]["cancelReason"], "Başka salon");

    // Tahsilat takvimi: kapanmamış taksitler; kısmen ödenen ve vadesi geçen işaretli.
    api.set_today(NaiveDate::from_ymd_opt(2026, 5, 10).unwrap());
    let due = api.get("/api/reports/due").await;
    let due = due.as_array().unwrap();
    assert_eq!(due.len(), 2);
    assert_eq!(due[0]["label"], "1. taksit");
    assert_eq!(due[0]["paid"], 30_000_00);
    assert_eq!(due[0]["state"], "gecikti");
    assert_eq!(due[1]["dueDate"], "2026-08-01");
    assert_eq!(due[1]["state"], "bekliyor");

    // Raporlar yalnızca yönetici içindir.
    api.ok(Method::POST, "/api/users", json!({"name": "Mehmet", "username": "mehmet", "password": "Salon2026", "role": "calisan", "isActive": true})).await;
    let staff = api.login("mehmet", "Salon2026").await;
    let (status, _) = api.call_as(&staff, Method::GET, "/api/reports/due", Value::Null).await;
    assert_eq!(status, StatusCode::FORBIDDEN);
}
