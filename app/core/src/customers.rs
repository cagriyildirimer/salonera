use rusqlite::{params, Connection, OptionalExtension};

use crate::auth::audit;
use crate::error::{rule, AppError, AppResult};
use crate::model::*;
use crate::reservations;
use crate::util::{self, fmt_datetime, new_id, optional, required};
use crate::Ctx;

const SELECT: &str = "SELECT c.id, c.full_name, c.phone, c.phone2, c.bride_name, c.groom_name, c.notes,
    (SELECT COUNT(*) FROM reservations r WHERE r.customer_id = c.id AND r.status <> 'iptal')
    FROM customers c";

fn map(r: &rusqlite::Row<'_>) -> rusqlite::Result<CustomerDto> {
    Ok(CustomerDto {
        id: r.get(0)?,
        full_name: r.get(1)?,
        phone: r.get(2)?,
        phone2: r.get(3)?,
        bride_name: r.get(4)?,
        groom_name: r.get(5)?,
        notes: r.get(6)?,
        reservation_count: r.get(7)?,
    })
}

/// Ad, gelin/damat adı veya telefonun bir kısmıyla arama. Boş arama son eklenenleri getirir.
pub fn list(conn: &Connection, query: Option<&str>) -> AppResult<Vec<CustomerDto>> {
    let query = query.map(str::trim).unwrap_or_default();
    if query.is_empty() {
        let mut stmt = conn.prepare(&format!("{SELECT} WHERE c.is_deleted = 0 ORDER BY c.created_at DESC LIMIT 200"))?;
        let rows = stmt.query_map([], map)?;
        return Ok(rows.collect::<Result<_, _>>()?);
    }
    let text = format!("%{}%", util::turkish_lower(query));
    let digits = util::digits(query);
    let phone = if digits.len() >= 3 { format!("%{digits}%") } else { "\u{0}".into() };
    let mut stmt = conn.prepare(&format!(
        "{SELECT} WHERE c.is_deleted = 0 AND (c.search_name LIKE ?1
            OR REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(c.phone, ' ', ''), '-', ''), '(', ''), ')', ''), '.', '') LIKE ?2
            OR REPLACE(REPLACE(REPLACE(COALESCE(c.phone2, ''), ' ', ''), '-', ''), '(', '') LIKE ?2)
         ORDER BY c.full_name LIMIT 200"
    ))?;
    let rows = stmt.query_map(params![text, phone], map)?;
    Ok(rows.collect::<Result<_, _>>()?)
}

pub fn get(conn: &Connection, id: &str) -> AppResult<CustomerDto> {
    conn.query_row(&format!("{SELECT} WHERE c.id = ?1 AND c.is_deleted = 0"), [id], map)
        .optional()?
        .ok_or_else(|| AppError::NotFound("Müşteri bulunamadı.".into()))
}

pub fn detail(conn: &Connection, id: &str, today: chrono::NaiveDate) -> AppResult<CustomerDetail> {
    let customer = get(conn, id)?;
    let reservations = reservations::load_summaries(conn, "r.customer_id = ?1 ORDER BY r.starts_at DESC", &[&id], today)?;
    Ok(CustomerDetail { customer, reservations })
}

pub fn ensure_exists(conn: &Connection, id: &str) -> AppResult<()> {
    get(conn, id).map(|_| ())
}

/// Arama için hem ad hem gelin/damat adları birlikte saklanır.
fn search_name(request: &SaveCustomerRequest, full_name: &str) -> String {
    let mut text = full_name.to_string();
    for extra in [&request.bride_name, &request.groom_name].into_iter().flatten() {
        text.push(' ');
        text.push_str(extra);
    }
    util::turkish_lower(&text)
}

pub fn insert(conn: &Connection, ctx: &Ctx, request: &SaveCustomerRequest) -> AppResult<String> {
    let full_name = required(&request.full_name, "Müşteri adı soyadı", 150)?;
    let phone = util::phone(&request.phone)?;
    let id = new_id();
    conn.execute(
        "INSERT INTO customers (id, full_name, search_name, phone, phone2, bride_name, groom_name, notes, created_at)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)",
        params![
            id,
            full_name,
            search_name(request, &full_name),
            phone,
            optional(&request.phone2, 30),
            optional(&request.bride_name, 100),
            optional(&request.groom_name, 100),
            optional(&request.notes, 2000),
            fmt_datetime(ctx.now)
        ],
    )?;
    audit(conn, ctx, "musteri_eklendi", &full_name)?;
    Ok(id)
}

pub fn update(conn: &Connection, ctx: &Ctx, id: &str, request: &SaveCustomerRequest) -> AppResult<CustomerDto> {
    ensure_exists(conn, id)?;
    let full_name = required(&request.full_name, "Müşteri adı soyadı", 150)?;
    let phone = util::phone(&request.phone)?;
    conn.execute(
        "UPDATE customers SET full_name = ?1, search_name = ?2, phone = ?3, phone2 = ?4, bride_name = ?5,
                groom_name = ?6, notes = ?7 WHERE id = ?8",
        params![
            full_name,
            search_name(request, &full_name),
            phone,
            optional(&request.phone2, 30),
            optional(&request.bride_name, 100),
            optional(&request.groom_name, 100),
            optional(&request.notes, 2000),
            id
        ],
    )?;
    audit(conn, ctx, "musteri_guncellendi", &full_name)?;
    get(conn, id)
}

/// Müşteri listeden kaldırılır; geçmiş kayıtlar silinmez. Aktif kaydı olan müşteri kaldırılamaz.
pub fn delete(conn: &Connection, ctx: &Ctx, id: &str) -> AppResult<()> {
    let customer = get(conn, id)?;
    let active: bool = conn.query_row(
        "SELECT EXISTS(SELECT 1 FROM reservations WHERE customer_id = ?1 AND status <> 'iptal' AND ends_at > ?2)",
        params![id, fmt_datetime(ctx.now)],
        |r| r.get(0),
    )?;
    if active {
        return rule("Bu müşterinin ileri tarihli bir rezervasyonu var. Önce rezervasyonu iptal edin.");
    }
    conn.execute("UPDATE customers SET is_deleted = 1 WHERE id = ?1", [id])?;
    audit(conn, ctx, "musteri_kaldirildi", &customer.full_name)
}
