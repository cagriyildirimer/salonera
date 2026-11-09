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

/// Paket ve ek hizmet aynı yapıdadır.
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProductDto {
    pub id: String,
    pub name: String,
    pub description: Option<String>,
    pub price: i64,
    pub is_active: bool,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SaveProductRequest {
    pub name: String,
    pub description: Option<String>,
    pub price: i64,
    pub is_active: bool,
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
    pub name: String,
    pub price: i64,
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
    pub package_price: i64,
    pub extras: Vec<ExtraLine>,
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
