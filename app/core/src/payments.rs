use chrono::NaiveDate;
use rusqlite::{params, Connection, OptionalExtension};

use crate::auth::audit;
use crate::error::{rule, AppError, AppResult};
use crate::model::*;
use crate::util::{self, ensure_amount, fmt_datetime, new_id, required};
use crate::Ctx;

const SELECT: &str = "SELECT p.id, p.reservation_id, r.no, p.no, p.date, p.amount, p.method, p.is_refund, p.note,
    p.is_cancelled, p.cancel_reason, u.name, c.full_name
    FROM payments p
    JOIN reservations r ON r.id = p.reservation_id
    JOIN customers c ON c.id = r.customer_id
    JOIN users u ON u.id = p.created_by";

fn map(r: &rusqlite::Row<'_>) -> rusqlite::Result<PaymentDto> {
    Ok(PaymentDto {
        id: r.get(0)?,
        reservation_id: r.get(1)?,
        reservation_no: r.get(2)?,
        no: r.get(3)?,
        date: r.get(4)?,
        amount: r.get(5)?,
        method: r.get(6)?,
        is_refund: r.get(7)?,
        note: r.get(8)?,
        is_cancelled: r.get(9)?,
        cancel_reason: r.get(10)?,
        created_by: r.get(11)?,
        customer_name: r.get(12)?,
    })
}

pub fn get(conn: &Connection, id: &str) -> AppResult<PaymentDto> {
    conn.query_row(&format!("{SELECT} WHERE p.id = ?1"), [id], map)
        .optional()?
        .ok_or_else(|| AppError::NotFound("Ödeme kaydı bulunamadı.".into()))
}

pub fn list_for_reservation(conn: &Connection, reservation_id: &str) -> AppResult<Vec<PaymentDto>> {
    let mut stmt = conn.prepare(&format!("{SELECT} WHERE p.reservation_id = ?1 ORDER BY p.date DESC"))?;
    let rows = stmt.query_map([reservation_id], map)?;
    Ok(rows.collect::<Result<_, _>>()?)
}

/// Tarih aralığındaki tahsilat ve iadeler (bitiş günü dahil).
pub fn list_between(conn: &Connection, from: NaiveDate, to: NaiveDate) -> AppResult<Vec<PaymentDto>> {
    let start = fmt_datetime(from.and_hms_opt(0, 0, 0).unwrap());
    let end = fmt_datetime(to.succ_opt().unwrap_or(to).and_hms_opt(0, 0, 0).unwrap());
    let mut stmt = conn.prepare(&format!("{SELECT} WHERE p.date >= ?1 AND p.date < ?2 ORDER BY p.date DESC"))?;
    let rows = stmt.query_map(params![start, end], map)?;
    Ok(rows.collect::<Result<_, _>>()?)
}

fn net_paid(conn: &Connection, reservation_id: &str) -> AppResult<i64> {
    Ok(conn.query_row(
        "SELECT COALESCE(SUM(CASE WHEN is_refund = 1 THEN -amount ELSE amount END), 0)
         FROM payments WHERE reservation_id = ?1 AND is_cancelled = 0",
        [reservation_id],
        |r| r.get(0),
    )?)
}

/// Ödeme kaydını yazar ve makbuz numarası verir. Kural kontrolleri çağıran tarafta yapılır.
pub(crate) fn insert(
    conn: &Connection,
    ctx: &Ctx,
    reservation_id: &str,
    amount: i64,
    method: Method,
    note: Option<&str>,
    is_refund: bool,
) -> AppResult<String> {
    ensure_amount(amount, "Tutar")?;
    let (status, total): (Status, i64) =
        conn.query_row("SELECT status, total FROM reservations WHERE id = ?1", [reservation_id], |r| {
            Ok((r.get(0)?, r.get(1)?))
        })?;
    let paid = net_paid(conn, reservation_id)?;
    if is_refund {
        if amount > paid {
            return rule(format!("İade, müşterinin ödediği tutardan ({}) büyük olamaz.", util::money(paid)));
        }
    } else {
        if status != Status::Confirmed {
            return rule(match status {
                Status::Option => "Ön rezervasyona ödeme alınamaz. Önce kaydı kesinleştirin.",
                _ => "İptal edilmiş rezervasyona ödeme alınamaz.",
            });
        }
        if amount > total - paid {
            return rule(format!("Ödeme, kalan borçtan ({}) büyük olamaz.", util::money((total - paid).max(0))));
        }
    }
    let count: i64 = conn.query_row("SELECT COUNT(*) FROM payments", [], |r| r.get(0))?;
    let id = new_id();
    let no = format!("{}-{:05}", if is_refund { "I" } else { "M" }, count + 1);
    conn.execute(
        "INSERT INTO payments (id, no, reservation_id, date, amount, method, is_refund, note, created_by)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)",
        params![
            id,
            no,
            reservation_id,
            fmt_datetime(ctx.now),
            amount,
            method,
            is_refund,
            note.map(str::trim).filter(|x| !x.is_empty()),
            ctx.user.id
        ],
    )?;
    audit(conn, ctx, if is_refund { "iade" } else { "odeme_alindi" }, format!("{no} {}", util::money(amount)))?;
    Ok(id)
}

pub fn take(conn: &mut Connection, ctx: &Ctx, reservation_id: &str, input: &PaymentInput) -> AppResult<String> {
    let tx = conn.transaction()?;
    let id = insert(&tx, ctx, reservation_id, input.amount, input.method, input.note.as_deref(), false)?;
    tx.commit()?;
    Ok(id)
}

pub fn refund(conn: &mut Connection, ctx: &Ctx, reservation_id: &str, request: &RefundRequest) -> AppResult<String> {
    let reason = required(&request.reason, "İade nedeni", 500)?;
    let tx = conn.transaction()?;
    let id = insert(&tx, ctx, reservation_id, request.amount, request.method, Some(&reason), true)?;
    tx.commit()?;
    Ok(id)
}

/// Hatalı girilen ödemeyi iptal eder (yalnızca yönetici). Kayıt silinmez, "iptal" olarak işaretlenir.
pub fn cancel(conn: &mut Connection, ctx: &Ctx, payment_id: &str, reason: &str) -> AppResult<()> {
    ctx.user.require_admin()?;
    let reason = required(reason, "İptal nedeni", 500)?;
    let tx = conn.transaction()?;
    let payment = get(&tx, payment_id)?;
    if payment.is_cancelled {
        return rule("Bu ödeme zaten iptal edilmiş.");
    }
    if !payment.is_refund && net_paid(&tx, &payment.reservation_id)? - payment.amount < 0 {
        return rule("Bu ödemeden iade yapılmış. Önce iadeyi iptal edin.");
    }
    tx.execute(
        "UPDATE payments SET is_cancelled = 1, cancel_reason = ?1 WHERE id = ?2",
        params![reason, payment_id],
    )?;
    audit(&tx, ctx, "odeme_iptal", format!("{}: {reason}", payment.no))?;
    tx.commit()?;
    Ok(())
}
