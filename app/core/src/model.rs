//! Ekranlarla sunucu arasında taşınan veriler. Bütün tutarlar KURUŞ (i64) cinsindendir;
//! ekran TL'ye çevirip gösterir. Saatler "SS:DD" biçiminde metindir. Tarihler "YYYY-AA-GG".

use chrono::NaiveDate;
use serde::{Deserialize, Serialize};

macro_rules! text_enum {
    ($name:ident { $($variant:ident = $text:literal),+ $(,)? }) => {
        #[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
        pub enum $name { $(#[serde(rename = $text)] $variant),+ }

        impl $name {
            pub fn as_str(self) -> &'static str { match self { $($name::$variant => $text),+ } }

            pub fn parse(value: &str) -> Option<Self> {
                match value { $($text => Some($name::$variant),)+ _ => None }
            }
        }

        impl rusqlite::types::ToSql for $name {
            fn to_sql(&self) -> rusqlite::Result<rusqlite::types::ToSqlOutput<'_>> {
                Ok(self.as_str().into())
            }
        }

        impl rusqlite::types::FromSql for $name {
            fn column_result(value: rusqlite::types::ValueRef<'_>) -> rusqlite::types::FromSqlResult<Self> {
                let text = value.as_str()?;
                $name::parse(text).ok_or(rusqlite::types::FromSqlError::InvalidType)
            }
        }
    };
}

text_enum!(Role { Admin = "yonetici", Staff = "calisan" });
text_enum!(EventKind {
    Wedding = "dugun", Engagement = "nisan", Henna = "kina", Promise = "soz",
    Circumcision = "sunnet", Cocktail = "kokteyl", Other = "diger",
});
text_enum!(Status { Option = "on_rezervasyon", Confirmed = "kesin", Cancelled = "iptal" });
text_enum!(Method { Cash = "nakit", Card = "kredi_karti", Transfer = "havale" });
text_enum!(InstallmentState { Waiting = "bekliyor", Partial = "kismen", Paid = "odendi", Late = "gecikti" });

// ---------------- Genel ----------------
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerInfo {
    pub business_name: String,
    pub is_set_up: bool,
    pub version: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct HallInput {
    pub name: String,
    pub capacity: i64,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SetupRequest {
    pub business_name: String,
    pub phone: Option<String>,
    pub address: Option<String>,
    pub admin_name: String,
    pub admin_username: String,
    pub admin_password: String,
    pub halls: Vec<HallInput>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LoginRequest {
    pub username: String,
    pub password: String,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct UserDto {
    pub id: String,
    pub name: String,
    pub username: String,
    pub role: Role,
    pub is_active: bool,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LoginResponse {
    pub token: String,
    pub user: UserDto,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LoginUserOption {
    pub name: String,
    pub username: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SaveUserRequest {
    pub name: String,
    pub username: String,
    pub password: Option<String>,
    pub role: Role,
    pub is_active: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ChangePasswordRequest {
    pub current_password: String,
    pub new_password: String,
}

// ---------------- Ayarlar ----------------
#[derive(Serialize, Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct BusinessSettings {
    pub business_name: String,
    pub phone: Option<String>,
    pub address: Option<String>,
    pub tax_office: Option<String>,
    pub tax_number: Option<String>,
    pub contract_terms: String,
    pub default_option_days: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HallDto {
    pub id: String,
    pub name: String,
    pub capacity: i64,
    pub is_active: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SaveHallRequest {
    pub name: String,
    pub capacity: i64,
    pub is_active: bool,
}

/// Paket ve ek hizmet aynı yapıdadır. `price` sabit tutardır; üzerine `per_guest_price × kişi` eklenir.
/// Paketlerde kişi = max(davetli, min_guests). Ek hizmetlerde `min_guests` 0'dır; kişi sayısı
/// paketin "en az kişi" sınırıyla hesaplanır. `group_name` yalnızca ek hizmetlerde: aynı gruptan tek seçim.
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProductDto {
    pub id: String,
    pub name: String,
    pub description: Option<String>,
    pub price: i64,
    pub per_guest_price: i64,
    pub min_guests: i64,
    pub group_name: Option<String>,
    pub is_active: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SaveProductRequest {
    pub name: String,
    pub description: Option<String>,
    pub price: i64,
    #[serde(default)]
    pub per_guest_price: i64,
    #[serde(default)]
    pub min_guests: i64,
    /// Ek hizmetin seçenek grubu (ör. "Pasta"); boşsa bağımsız ek hizmettir.
    #[serde(default)]
    pub group_name: Option<String>,
    pub is_active: bool,
}

/// Paket fiyatlandırması: sabit tutar + kişi başı × ücretlendirilen kişi sayısı.
#[derive(Serialize, Clone, Copy, Debug, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct PackagePricing {
    pub base_price: i64,
    pub per_guest_price: i64,
    pub min_guests: i64,
}

impl PackagePricing {
    /// Ücretlendirilen kişi sayısı: davetli sayısı, "en az kişi" sınırının altındaysa sınır kadar.
    pub fn billed_guests(&self, guests: i64) -> i64 {
        if self.per_guest_price == 0 { 0 } else { guests.max(self.min_guests) }
    }

    pub fn total(&self, guests: i64) -> i64 {
        self.base_price + self.per_guest_price * self.billed_guests(guests)
    }
}

/// Menü farkının çarpıldığı kişi sayısı: davetli, paketin "en az kişi" sınırının altındaysa sınır kadar.
pub fn menu_total(per_guest_price: i64, guests: i64, min_guests: i64) -> i64 {
    per_guest_price * guests.max(min_guests)
}

// ---------------- Yemek menüsü ----------------
/// Menüdeki bir yemek, ör. "Ana yemek" – "Kuzu tandır".
#[derive(Serialize, Deserialize, Clone, Debug, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct MenuDish {
    pub course: String,
    pub name: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct MenuDto {
    pub id: String,
    pub name: String,
    pub description: Option<String>,
    /// Kişi başı fark (0 = paket fiyatına dahil).
    pub per_guest_price: i64,
    pub is_active: bool,
    pub items: Vec<MenuDish>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SaveMenuRequest {
    pub name: String,
    pub description: Option<String>,
    #[serde(default)]
    pub per_guest_price: i64,
    pub is_active: bool,
    #[serde(default)]
    pub items: Vec<MenuDish>,
}

/// Rezervasyonda katalogdan menü seçmek (veya `menu_id: null` ile menüyü kaldırmak).
/// Fiyat farkı toplam tutara ve ödenmemiş taksitlere yansır.
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SelectMenuRequest {
    pub menu_id: Option<String>,
}

/// Rezervasyona kopyalanmış menünün o düğüne özel içeriği (fiyatı değiştirmez).
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SaveReservationMenuRequest {
    #[serde(default)]
    pub items: Vec<MenuDish>,
    pub notes: Option<String>,
}

// ---------------- Müşteri ----------------
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CustomerDto {
    pub id: String,
    pub full_name: String,
    pub phone: String,
    pub phone2: Option<String>,
    pub bride_name: Option<String>,
    pub groom_name: Option<String>,
    pub notes: Option<String>,
    pub reservation_count: i64,
}

#[derive(Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct SaveCustomerRequest {
    pub full_name: String,
    pub phone: String,
    pub phone2: Option<String>,
    pub bride_name: Option<String>,
    pub groom_name: Option<String>,
    pub notes: Option<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CustomerDetail {
    pub customer: CustomerDto,
    pub reservations: Vec<ReservationSummary>,
}

// ---------------- Rezervasyon ----------------
#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct ReservationSummary {
    pub id: String,
    pub no: String,
    pub date: NaiveDate,
    pub start: String,
    pub end: String,
    pub hall_id: String,
    pub hall_name: String,
    pub customer_id: String,
    pub customer_name: String,
    pub customer_phone: String,
    pub kind: EventKind,
    pub guest_count: i64,
    pub status: Status,
    pub option_until: Option<NaiveDate>,
    pub option_expired: bool,
    pub total: i64,
    pub paid: i64,
    pub remaining: i64,
    pub overdue: i64,
}

#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct ExtraLine {
    /// Katalogdaki ek hizmet (eski kayıtlarda boş olabilir).
    #[serde(default)]
    pub extra_id: Option<String>,
    pub name: String,
    #[serde(default)]
    pub group_name: Option<String>,
    /// Kişi sayısıyla hesaplanmış tutar (sözleşmeye yazılan).
    pub price: i64,
    /// Satış anındaki sabit tutar ve kişi başı fiyat.
    #[serde(default)]
    pub base_price: i64,
    #[serde(default)]
    pub per_guest: i64,
}

impl ExtraLine {
    /// Tutarı verilen kişi sayısıyla yeniden hesaplar (kişi başı fiyatı yoksa değişmez).
    pub fn priced_for(&self, guests: i64, min_guests: i64) -> i64 {
        if self.per_guest == 0 { self.price } else { self.base_price + menu_total(self.per_guest, guests, min_guests) }
    }
}

/// Kesin kayıtta ek hizmetleri / seçenekleri değiştirmek (ör. tadımdan sonra pasta değişti).
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ChangeExtrasRequest {
    #[serde(default)]
    pub extra_ids: Vec<String>,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct InstallmentDto {
    pub id: String,
    pub label: String,
    pub due_date: NaiveDate,
    pub amount: i64,
    pub paid: i64,
    pub state: InstallmentState,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct PaymentDto {
    pub id: String,
    pub reservation_id: String,
    pub reservation_no: String,
    pub no: String,
    pub date: String,
    pub amount: i64,
    pub method: Method,
    pub is_refund: bool,
    pub note: Option<String>,
    pub is_cancelled: bool,
    pub cancel_reason: Option<String>,
    pub created_by: String,
    pub customer_name: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ReservationDetail {
    pub summary: ReservationSummary,
    pub package_name: Option<String>,
    /// Kişi sayısıyla hesaplanmış paket tutarı (sözleşmeye yazılan).
    pub package_price: i64,
    /// Satış anındaki paket fiyatlandırması (paket sonradan değişse de bu kalır).
    pub package_pricing: PackagePricing,
    pub extras: Vec<ExtraLine>,
    /// Seçilen yemek menüsü (satış anındaki adı ve kişi başı farkı); yoksa `None`.
    pub menu_name: Option<String>,
    pub menu_per_guest: i64,
    /// Kişi sayısıyla hesaplanmış menü farkı (sözleşmeye yazılan).
    pub menu_price: i64,
    /// Bu düğünün yemekleri (katalogdan kopyalanıp düzenlenmiş olabilir).
    pub menu_items: Vec<MenuDish>,
    /// Mutfağa notlar: alerji, çocuk / vejetaryen menü sayısı…
    pub menu_notes: Option<String>,
    pub discount: i64,
    pub notes: Option<String>,
    pub cancel_reason: Option<String>,
    pub installments: Vec<InstallmentDto>,
    pub payments: Vec<PaymentDto>,
    pub created_by: String,
    pub created_at: String,
    pub confirmed_at: Option<String>,
}

#[derive(Deserialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct InstallmentInput {
    pub label: String,
    pub due_date: NaiveDate,
    pub amount: i64,
}

#[derive(Deserialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct PaymentInput {
    pub amount: i64,
    pub method: Method,
    pub note: Option<String>,
}

/// Paket, ek hizmetler, indirim, ödeme planı ve (varsa) hemen alınan kapora.
#[derive(Deserialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct SaleInput {
    pub package_id: String,
    #[serde(default)]
    pub extra_ids: Vec<String>,
    /// Yemek menüsü (isteğe bağlı; sonradan da seçilebilir).
    #[serde(default)]
    pub menu_id: Option<String>,
    #[serde(default)]
    pub discount: i64,
    /// Boş bırakılırsa toplam tutar düğünden 7 gün önceye tek ödeme olarak planlanır.
    #[serde(default)]
    pub plan: Vec<InstallmentInput>,
    pub first_payment: Option<PaymentInput>,
}

#[derive(Deserialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct CreateReservationRequest {
    pub customer_id: Option<String>,
    pub new_customer: Option<SaveCustomerRequest>,
    pub hall_id: String,
    pub date: NaiveDate,
    pub start: String,
    pub end: String,
    pub kind: EventKind,
    pub guest_count: i64,
    pub notes: Option<String>,
    /// Ön rezervasyonsa tarihin kaç gün tutulacağı. `sale` ile birlikte verilemez.
    pub option_days: Option<i64>,
    pub sale: Option<SaleInput>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateReservationResponse {
    pub reservation_id: String,
    pub no: String,
    pub payment_id: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateReservationRequest {
    pub hall_id: String,
    pub date: NaiveDate,
    pub start: String,
    pub end: String,
    pub kind: EventKind,
    pub guest_count: i64,
    pub notes: Option<String>,
    /// Kesin kayıtta davetli sayısı değiştiyse paket fiyatı, toplam ve kalan taksitler
    /// yeni kişi sayısına göre yeniden hesaplansın mı? (Ekran kullanıcıya sorar.)
    #[serde(default)]
    pub reprice: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ReasonRequest {
    pub reason: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExtendOptionRequest {
    pub days: i64,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SetPlanRequest {
    pub plan: Vec<InstallmentInput>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RefundRequest {
    pub amount: i64,
    pub method: Method,
    pub reason: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HallAvailability {
    pub hall_id: String,
    pub hall_name: String,
    pub capacity: i64,
    pub is_free: bool,
    pub busy_with: Option<String>,
}

// ---------------- Ana sayfa ve raporlar ----------------
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Dashboard {
    pub today: Vec<ReservationSummary>,
    pub upcoming: Vec<ReservationSummary>,
    pub options: Vec<ReservationSummary>,
    pub overdue: Vec<ReservationSummary>,
    pub week_count: i64,
    pub month_collected: i64,
    pub overdue_total: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct MonthRow {
    pub month: u32,
    pub event_count: i64,
    pub sales: i64,
    pub collected: i64,
    pub refunded: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct YearReport {
    pub year: i32,
    pub months: Vec<MonthRow>,
    pub total_sales: i64,
    pub total_collected: i64,
    pub total_refunded: i64,
}

/// Satış analizi satırı: tarihi aralıkta olan her kayıt (iptal ve ön rezervasyonlar dahil).
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SalesRow {
    pub id: String,
    pub no: String,
    pub date: NaiveDate,
    pub hall_name: String,
    pub customer_name: String,
    pub kind: EventKind,
    pub guest_count: i64,
    pub status: Status,
    pub package_name: Option<String>,
    pub package_price: i64,
    pub menu_name: Option<String>,
    pub menu_price: i64,
    pub discount: i64,
    pub total: i64,
    pub paid: i64,
    pub extras: Vec<SalesExtra>,
    /// Kaydın açıldığı gün.
    pub created_on: NaiveDate,
    pub created_by: String,
    pub cancel_reason: Option<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SalesExtra {
    pub name: String,
    pub group_name: Option<String>,
    pub price: i64,
}

/// Tahsilat takvimi: kesin kayıtların henüz kapanmamış taksitleri (gecikenler dahil).
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DueRow {
    pub reservation_id: String,
    pub no: String,
    pub customer_name: String,
    pub customer_phone: String,
    pub event_date: NaiveDate,
    pub hall_name: String,
    pub label: String,
    pub due_date: NaiveDate,
    pub amount: i64,
    pub paid: i64,
    pub state: InstallmentState,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HallUsageRow {
    pub hall_name: String,
    pub event_count: i64,
    pub sales: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct BackupInfo {
    pub file_name: String,
    pub created_at: String,
    pub size_bytes: u64,
}

// ---------------- Organizasyon planı ----------------
/// Saatlik akışta bir satır, ör. 21:00 – Pasta kesimi.
#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct ProgramItem {
    /// "SS:DD"
    pub time: String,
    pub title: String,
    pub note: Option<String>,
}

/// Müşterinin seçtiği müzik, ör. "İlk dans müziği" – şarkı adı – bağlantı.
#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct MusicItem {
    pub label: String,
    pub song: Option<String>,
    pub link: Option<String>,
}

#[derive(Serialize, Deserialize, Clone, Debug, Default)]
#[serde(rename_all = "camelCase")]
pub struct Program {
    #[serde(default)]
    pub items: Vec<ProgramItem>,
    #[serde(default)]
    pub music: Vec<MusicItem>,
    /// Plana dair genel notlar (masa düzeni, süsleme rengi…).
    pub notes: Option<String>,
}
