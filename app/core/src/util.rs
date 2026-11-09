use chrono::{Duration, NaiveDate, NaiveDateTime, NaiveTime};
use uuid::Uuid;

use crate::error::{rule, AppResult};

pub const DATETIME_FORMAT: &str = "%Y-%m-%dT%H:%M:%S";

pub fn new_id() -> String {
    Uuid::new_v4().to_string()
}

/// 256 bit rastgele oturum anahtarı.
pub fn new_token() -> String {
    format!("{}{}", Uuid::new_v4().simple(), Uuid::new_v4().simple())
}

pub fn fmt_datetime(value: NaiveDateTime) -> String {
    value.format(DATETIME_FORMAT).to_string()
}

pub fn parse_datetime(value: &str) -> NaiveDateTime {
    NaiveDateTime::parse_from_str(value, DATETIME_FORMAT).unwrap_or_default()
}

pub fn fmt_date(value: NaiveDate) -> String {
    value.format("%Y-%m-%d").to_string()
}

pub fn parse_date(value: &str) -> NaiveDate {
    NaiveDate::parse_from_str(value, "%Y-%m-%d").unwrap_or_default()
}

pub fn parse_time(value: &str, field: &str) -> AppResult<NaiveTime> {
    match NaiveTime::parse_from_str(value.trim(), "%H:%M") {
        Ok(time) => Ok(time),
        Err(_) => rule(format!("{field} saati geçersiz (örnek: 19:00).")),
    }
}

/// Başlangıç ve bitiş anlarını hesaplar. Bitiş saati başlangıçtan önce ya da eşitse
/// organizasyon gece yarısını geçiyor demektir; bitiş ertesi güne yazılır.
pub fn event_range(date: NaiveDate, start: &str, end: &str) -> AppResult<(NaiveDateTime, NaiveDateTime)> {
    let start_time = parse_time(start, "Başlangıç")?;
    let end_time = parse_time(end, "Bitiş")?;
    let starts = date.and_time(start_time);
    let mut ends = date.and_time(end_time);
    if ends <= starts {
        ends += Duration::days(1);
    }
    if ends - starts > Duration::hours(18) {
        return rule("Organizasyon süresi 18 saatten uzun olamaz. Saatleri kontrol edin.");
    }
    Ok((starts, ends))
}

pub fn required(value: &str, field: &str, max: usize) -> AppResult<String> {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        return rule(format!("{field} boş bırakılamaz."));
    }
    if trimmed.chars().count() > max {
        return rule(format!("{field} en fazla {max} karakter olabilir."));
    }
    Ok(trimmed.to_string())
}

pub fn optional(value: &Option<String>, max: usize) -> Option<String> {
    let trimmed = value.as_deref().map(str::trim).unwrap_or_default();
    if trimmed.is_empty() {
        None
    } else {
        Some(trimmed.chars().take(max).collect())
    }
}

pub fn phone(value: &str) -> AppResult<String> {
    let text = required(value, "Telefon", 30)?;
    if text.chars().filter(char::is_ascii_digit).count() < 10 {
        return rule("Telefon numarası eksik görünüyor (örnek: 0532 123 45 67).");
    }
    Ok(text)
}

/// Türkçe küçük harf: "IŞIK" → "ışık", "İPEK" → "ipek".
pub fn turkish_lower(value: &str) -> String {
    value
        .trim()
        .chars()
        .flat_map(|c| match c {
            'I' => vec!['ı'],
            'İ' => vec!['i'],
            other => other.to_lowercase().collect(),
        })
        .collect()
}

/// Sadece rakamlar; telefonla aramada boşluk ve tire farkını yok eder.
pub fn digits(value: &str) -> String {
    value.chars().filter(char::is_ascii_digit).collect()
}

pub fn money(kurus: i64) -> String {
    let negative = kurus < 0;
    let abs = kurus.unsigned_abs();
    let lira = abs / 100;
    let cents = abs % 100;
    let mut grouped = String::new();
    for (index, ch) in lira.to_string().chars().rev().enumerate() {
        if index > 0 && index % 3 == 0 {
            grouped.push('.');
        }
        grouped.push(ch);
    }
    let lira_text: String = grouped.chars().rev().collect();
    format!("{}{lira_text},{cents:02} TL", if negative { "-" } else { "" })
}

pub fn ensure_amount(amount: i64, field: &str) -> AppResult<()> {
    if amount <= 0 {
        return rule(format!("{field} sıfırdan büyük olmalıdır."));
    }
    if amount > 100_000_000_000 {
        return rule(format!("{field} çok büyük görünüyor. Kontrol edin."));
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn midnight_crossing_moves_end_to_next_day() {
        let date = NaiveDate::from_ymd_opt(2026, 6, 13).unwrap();
        let (start, end) = event_range(date, "19:00", "01:30").unwrap();
        assert_eq!(start.to_string(), "2026-06-13 19:00:00");
        assert_eq!(end.to_string(), "2026-06-14 01:30:00");
    }

    #[test]
    fn money_is_formatted_turkish_style() {
        assert_eq!(money(123_456_789), "1.234.567,89 TL");
        assert_eq!(money(5), "0,05 TL");
    }

    #[test]
    fn turkish_lowercase_handles_dotted_i() {
        assert_eq!(turkish_lower("IŞIK İPEK"), "ışık ipek");
    }
}
