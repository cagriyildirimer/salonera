//! Organizasyon planı: saatlik akış (yemek, pasta, takı töreni…) ve müşterinin seçtiği müzikler.

use chrono::Timelike;
use rusqlite::{params, Connection, OptionalExtension};

use crate::auth::audit;
use crate::error::{rule, AppError, AppResult};
use crate::model::*;
use crate::util::{self, new_id, optional, parse_datetime, required};
use crate::Ctx;

const MAX_ROWS: usize = 80;

struct Event {
    no: String,
    status: Status,
    /// Gün içindeki dakika: başlangıç ve bitiş.
    start: u32,
    end: u32,
    crosses_midnight: bool,
}

fn event(conn: &Connection, id: &str) -> AppResult<Event> {
    conn.query_row("SELECT no, status, starts_at, ends_at FROM reservations WHERE id = ?1", [id], |r| {
        let starts = parse_datetime(&r.get::<_, String>(2)?);
        let ends = parse_datetime(&r.get::<_, String>(3)?);
        Ok(Event {
            no: r.get(0)?,
            status: r.get(1)?,
            start: starts.hour() * 60 + starts.minute(),
            end: ends.hour() * 60 + ends.minute(),
            crosses_midnight: ends.date() > starts.date(),
        })
    })
    .optional()?
    .ok_or_else(|| AppError::NotFound("Rezervasyon bulunamadı.".into()))
}

/// Akıştaki sıralama anahtarı: gece yarısını geçen organizasyonda 00:30 gibi saatler sona gelir.
fn sort_key(event: &Event, minutes: u32) -> u32 {
    if event.crosses_midnight && minutes < event.start && minutes <= event.end {
        minutes + 24 * 60
    } else {
        minutes
    }
}

pub fn get(conn: &Connection, id: &str) -> AppResult<Program> {
    let notes: Option<String> = conn
        .query_row("SELECT program_notes FROM reservations WHERE id = ?1", [id], |r| r.get(0))
        .optional()?
        .ok_or_else(|| AppError::NotFound("Rezervasyon bulunamadı.".into()))?;
    let mut stmt = conn.prepare("SELECT time, title, note FROM program_items WHERE reservation_id = ?1 ORDER BY ord")?;
    let items = stmt
        .query_map([id], |r| Ok(ProgramItem { time: r.get(0)?, title: r.get(1)?, note: r.get(2)? }))?
        .collect::<Result<Vec<_>, _>>()?;
    let mut stmt = conn.prepare("SELECT label, song, link FROM music_items WHERE reservation_id = ?1 ORDER BY ord")?;
    let music = stmt
        .query_map([id], |r| Ok(MusicItem { label: r.get(0)?, song: r.get(1)?, link: r.get(2)? }))?
        .collect::<Result<Vec<_>, _>>()?;
    Ok(Program { items, music, notes })
}

/// Bağlantıyı denetler: yalnızca web adresi kabul edilir ("www." ile başlıyorsa https:// eklenir).
fn link(value: &Option<String>, label: &str) -> AppResult<Option<String>> {
    let Some(text) = optional(value, 500) else { return Ok(None) };
    let text = if text.to_lowercase().starts_with("www.") { format!("https://{text}") } else { text };
    let lower = text.to_lowercase();
    if !(lower.starts_with("https://") || lower.starts_with("http://")) || text.contains(char::is_whitespace) {
        return rule(format!("“{label}” bağlantısı geçersiz. Bağlantıyı tarayıcıdan kopyalayıp yapıştırın (https:// ile başlar)."));
    }
    Ok(Some(text))
}

/// Planın tamamını kaydeder (eski satırların yerine geçer). Akış saate göre sıralanır.
pub fn save(conn: &mut Connection, ctx: &Ctx, id: &str, program: &Program) -> AppResult<Program> {
    let tx = conn.transaction()?;
    let event = event(&tx, id)?;
    if event.status == Status::Cancelled {
        return rule("İptal edilmiş rezervasyonun planı değiştirilemez.");
    }
    if program.items.len() > MAX_ROWS || program.music.len() > MAX_ROWS {
        return rule(format!("Planda en fazla {MAX_ROWS} satır olabilir."));
    }

    let mut items = Vec::with_capacity(program.items.len());
    for item in &program.items {
        let title = required(&item.title, "Akıştaki her satırın açıklaması", 120)?;
        let time = util::parse_time(&item.time, &format!("“{title}”"))?;
        let minutes = time.hour() * 60 + time.minute();
        items.push((sort_key(&event, minutes), time.format("%H:%M").to_string(), title, optional(&item.note, 300)));
    }
    items.sort_by_key(|x| x.0); // aynı saatteki satırlar yazıldığı sırada kalır

    let mut music = Vec::with_capacity(program.music.len());
    for item in &program.music {
        let label = required(&item.label, "Her müziğin ne zaman çalınacağı (ör. Giriş müziği)", 80)?;
        let song = optional(&item.song, 200);
        let link = link(&item.link, &label)?;
        if song.is_none() && link.is_none() {
            return rule(format!("“{label}” için şarkı adını veya bağlantısını yazın ya da satırı silin."));
        }
        music.push((label, song, link));
    }

    tx.execute("DELETE FROM program_items WHERE reservation_id = ?1", [id])?;
    for (ord, (_, time, title, note)) in items.iter().enumerate() {
        tx.execute(
            "INSERT INTO program_items (id, reservation_id, time, title, note, ord) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![new_id(), id, time, title, note, ord as i64],
        )?;
    }
    tx.execute("DELETE FROM music_items WHERE reservation_id = ?1", [id])?;
    for (ord, (label, song, link)) in music.iter().enumerate() {
        tx.execute(
            "INSERT INTO music_items (id, reservation_id, label, song, link, ord) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![new_id(), id, label, song, link, ord as i64],
        )?;
    }
    tx.execute(
        "UPDATE reservations SET program_notes = ?1 WHERE id = ?2",
        params![optional(&program.notes, 2000), id],
    )?;
    audit(&tx, ctx, "organizasyon_plani_guncellendi", &event.no)?;
    let saved = get(&tx, id)?;
    tx.commit()?;
    Ok(saved)
}
