//! Bir rezervasyonun takvimde nasıl görüneceği: başlık, açıklama, yer, saat.
//! Para bilgisi (fiyat, ödeme, kalan borç) takvime hiç yazılmaz.

use chrono::{NaiveDate, NaiveDateTime};
use serde::Serialize;
use sha2::{Digest, Sha256};

use crate::model::{EventKind, Status};

/// Takvime gönderilecek etkinlik. İçeriği değişmediyse (aynı `hash`) takvime tekrar yazılmaz.
#[derive(Clone, Debug, PartialEq, Serialize)]
pub struct CalendarEvent {
    pub title: String,
    pub description: String,
    pub location: String,
    pub start: NaiveDateTime,
    pub end: NaiveDateTime,
    /// Ön rezervasyon: takvimde "belirsiz / opsiyon" olarak görünür.
    pub tentative: bool,
    /// Salonun sırası; Google'da salon başına ayrı renk verilir.
    pub hall_index: usize,
}

impl CalendarEvent {
    pub fn hash(&self) -> String {
        let text = serde_json::to_string(self).unwrap_or_default();
        Sha256::digest(text.as_bytes()).iter().take(16).map(|b| format!("{b:02x}")).collect()
    }
}

/// Etkinliği oluşturmak için rezervasyondan okunan bilgiler.
pub struct EventSource {
    pub no: String,
    pub start: NaiveDateTime,
    pub end: NaiveDateTime,
    pub kind: EventKind,
    pub guest_count: i64,
    pub status: Status,
    pub option_until: Option<NaiveDate>,
    pub package_name: Option<String>,
    pub menu_name: Option<String>,
    pub hall_name: String,
    pub hall_index: usize,
    pub customer_name: String,
    pub phone: String,
    pub phone2: Option<String>,
    pub bride_name: Option<String>,
    pub groom_name: Option<String>,
    pub extras: Vec<String>,
}

pub fn kind_label(kind: EventKind) -> &'static str {
    match kind {
        EventKind::Wedding => "Düğün",
        EventKind::Engagement => "Nişan",
        EventKind::Henna => "Kına",
        EventKind::Promise => "Söz",
        EventKind::Circumcision => "Sünnet",
        EventKind::Cocktail => "Kokteyl",
        EventKind::Other => "Organizasyon",
    }
}

fn filled(value: &Option<String>) -> Option<&str> {
    value.as_deref().map(str::trim).filter(|v| !v.is_empty())
}

pub fn render(src: &EventSource, business_name: &str, address: Option<&str>) -> CalendarEvent {
    let tentative = src.status == Status::Option;
    // "Zeynep & Emre" çifti yazılıysa onu, yoksa müşterinin adını kullan.
    let who = match (filled(&src.bride_name), filled(&src.groom_name)) {
        (Some(b), Some(g)) => format!("{b} & {g}"),
        _ => src.customer_name.clone(),
    };
    let title = format!(
        "{}{} · {} — {}",
        if tentative { "(Opsiyon) " } else { "" },
        kind_label(src.kind),
        who,
        src.hall_name
    );

    let mut lines = vec![format!("Kayıt no: {}", src.no)];
    lines.push(match (tentative, src.option_until) {
        (true, Some(until)) => format!("Durum: Ön rezervasyon (opsiyon {} tarihine kadar)", until.format("%d.%m.%Y")),
        (true, None) => "Durum: Ön rezervasyon".to_string(),
        _ => "Durum: Kesin kayıt".to_string(),
    });
    lines.push(format!("Müşteri: {}", src.customer_name));
    let phones: Vec<&str> = std::iter::once(src.phone.trim()).chain(filled(&src.phone2)).filter(|p| !p.is_empty()).collect();
    if !phones.is_empty() {
        lines.push(format!("Telefon: {}", phones.join(" / ")));
    }
    if let (Some(b), Some(g)) = (filled(&src.bride_name), filled(&src.groom_name)) {
        lines.push(format!("Gelin / damat: {b} / {g}"));
    }
    lines.push(format!("Salon: {}", src.hall_name));
    lines.push(format!("Saat: {} – {}", src.start.format("%H:%M"), src.end.format("%H:%M")));
    lines.push(format!("Davetli: {} kişi", src.guest_count));
    if let Some(p) = filled(&src.package_name) {
        lines.push(format!("Paket: {p}"));
    }
    if let Some(m) = filled(&src.menu_name) {
        lines.push(format!("Menü: {m}"));
    }
    if !src.extras.is_empty() {
        lines.push(format!("Ek hizmetler: {}", src.extras.join(", ")));
    }
    lines.push(String::new());
    lines.push(
        "Bu etkinlik Salonera'dan otomatik eklenir. Değişiklikleri Salonera'da yapın; \
         takvimde yapılan değişiklikler Salonera'ya geçmez ve sonraki eşitlemede üzerine yazılabilir."
            .to_string(),
    );

    let mut location = format!("{}, {}", src.hall_name, business_name.trim());
    if let Some(a) = address.map(str::trim).filter(|a| !a.is_empty()) {
        location.push_str(", ");
        location.push_str(a);
    }

    CalendarEvent {
        title,
        description: lines.join("\n"),
        location,
        start: src.start,
        end: src.end,
        tentative,
        hall_index: src.hall_index,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn source() -> EventSource {
        let day = NaiveDate::from_ymd_opt(2026, 6, 13).unwrap();
        EventSource {
            no: "2026-0007".into(),
            start: day.and_hms_opt(19, 0, 0).unwrap(),
            end: day.and_hms_opt(23, 30, 0).unwrap(),
            kind: EventKind::Wedding,
            guest_count: 350,
            status: Status::Confirmed,
            option_until: None,
            package_name: Some("Altın paket".into()),
            menu_name: Some("Klasik menü".into()),
            hall_name: "Kristal Salon".into(),
            hall_index: 0,
            customer_name: "Ayşe Yılmaz".into(),
            phone: "0532 111 22 33".into(),
            phone2: None,
            bride_name: Some("Zeynep".into()),
            groom_name: Some("Emre".into()),
            extras: vec!["Pasta: Çilekli".into(), "DJ".into()],
        }
    }

    #[test]
    fn confirmed_event_text_has_no_money() {
        let e = render(&source(), "Kristal Düğün Salonu", Some("Ankara"));
        assert_eq!(e.title, "Düğün · Zeynep & Emre — Kristal Salon");
        assert!(e.description.contains("Telefon: 0532 111 22 33"));
        assert!(e.description.contains("Ek hizmetler: Pasta: Çilekli, DJ"));
        assert!(!e.description.contains('₺') && !e.description.contains("TL"));
        assert_eq!(e.location, "Kristal Salon, Kristal Düğün Salonu, Ankara");
        assert!(!e.tentative);
    }

    #[test]
    fn option_is_marked_and_hash_changes_with_content() {
        let mut s = source();
        s.status = Status::Option;
        s.option_until = NaiveDate::from_ymd_opt(2026, 3, 20);
        s.bride_name = None;
        let e = render(&s, "Kristal", None);
        assert_eq!(e.title, "(Opsiyon) Düğün · Ayşe Yılmaz — Kristal Salon");
        assert!(e.description.contains("opsiyon 20.03.2026 tarihine kadar"));
        assert!(e.tentative);
        let same = render(&s, "Kristal", None);
        assert_eq!(e.hash(), same.hash());
        s.guest_count = 351;
        assert_ne!(e.hash(), render(&s, "Kristal", None).hash());
    }
}
