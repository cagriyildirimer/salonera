use chrono::{Datelike, Duration, NaiveDate};
use rusqlite::{params, Connection};

use crate::error::{rule, AppResult};
use crate::model::*;
use crate::reservations::load_summaries;
use crate::util::{fmt_date, fmt_datetime};
use crate::Ctx;

fn midnight(date: NaiveDate) -> String {
    fmt_datetime(date.and_hms_opt(0, 0, 0).unwrap())
}

pub fn dashboard(conn: &Connection, today: NaiveDate) -> AppResult<Dashboard> {
    let tomorrow = midnight(today + Duration::days(1));
    let today_text = midnight(today);
    let month_end = midnight(today + Duration::days(31));
    let week_end = midnight(today + Duration::days(7));

    let today_list = load_summaries(
        conn,
        "r.status <> 'iptal' AND r.starts_at >= ?1 AND r.starts_at < ?2 ORDER BY r.starts_at",
        &[&today_text, &tomorrow],
        today,
    )?;
    let upcoming = load_summaries(
        conn,
        "r.status <> 'iptal' AND r.starts_at >= ?1 AND r.starts_at < ?2 ORDER BY r.starts_at LIMIT 12",
        &[&tomorrow, &month_end],
        today,
    )?;
    // Süresi 3 gün içinde dolacak veya dolmuş, ama organizasyonu henüz geçmemiş ön rezervasyonlar.
    let soon = fmt_date(today + Duration::days(3));
    let options = load_summaries(
        conn,
        "r.status = 'on_rezervasyon' AND r.option_until <= ?1 AND r.starts_at >= ?2 ORDER BY r.option_until",
        &[&soon, &today_text],
        today,
    )?;
    let mut overdue: Vec<ReservationSummary> =
        load_summaries(conn, "r.status = 'kesin' AND r.total > 0 ORDER BY r.starts_at", &[], today)?
            .into_iter()
            .filter(|x| x.overdue > 0)
            .collect();
    overdue.sort_by_key(|x| -x.overdue);
    let overdue_total = overdue.iter().map(|x| x.overdue).sum();

    let week_count: i64 = conn.query_row(
        "SELECT COUNT(*) FROM reservations WHERE status <> 'iptal' AND starts_at >= ?1 AND starts_at < ?2",
        params![today_text, week_end],
        |r| r.get(0),
    )?;
    let month_start = today.with_day(1).unwrap();
    let month_collected: i64 = conn.query_row(
        "SELECT COALESCE(SUM(CASE WHEN is_refund = 1 THEN -amount ELSE amount END), 0)
         FROM payments WHERE is_cancelled = 0 AND date >= ?1 AND date < ?2",
        params![midnight(month_start), tomorrow],
        |r| r.get(0),
    )?;
    Ok(Dashboard { today: today_list, upcoming, options, overdue, week_count, month_collected, overdue_total })
}

/// Aylık özet: satış = o ay yapılacak kesin organizasyonların tutarı; tahsilat = o ay alınan para.
pub fn year(conn: &Connection, ctx: &Ctx, year: i32) -> AppResult<YearReport> {
    ctx.user.require_admin()?;
    if !(2000..=2100).contains(&year) {
        return rule("Geçerli bir yıl seçin.");
    }
    let mut months = Vec::with_capacity(12);
    for month in 1..=12u32 {
        let start = NaiveDate::from_ymd_opt(year, month, 1).unwrap();
        let end = if month == 12 {
            NaiveDate::from_ymd_opt(year + 1, 1, 1).unwrap()
        } else {
            NaiveDate::from_ymd_opt(year, month + 1, 1).unwrap()
        };
        let (s, e) = (midnight(start), midnight(end));
        let (event_count, sales): (i64, i64) = conn.query_row(
            "SELECT COUNT(*), COALESCE(SUM(total), 0) FROM reservations
             WHERE status = 'kesin' AND starts_at >= ?1 AND starts_at < ?2",
            params![s, e],
            |r| Ok((r.get(0)?, r.get(1)?)),
        )?;
        let (collected, refunded): (i64, i64) = conn.query_row(
            "SELECT COALESCE(SUM(CASE WHEN is_refund = 0 THEN amount END), 0),
                    COALESCE(SUM(CASE WHEN is_refund = 1 THEN amount END), 0)
             FROM payments WHERE is_cancelled = 0 AND date >= ?1 AND date < ?2",
            params![s, e],
            |r| Ok((r.get(0)?, r.get(1)?)),
        )?;
        months.push(MonthRow { month, event_count, sales, collected, refunded });
    }
    Ok(YearReport {
        year,
        total_sales: months.iter().map(|m| m.sales).sum(),
        total_collected: months.iter().map(|m| m.collected).sum(),
        total_refunded: months.iter().map(|m| m.refunded).sum(),
        months,
    })
}

/// Alacaklar: ödemesi tamamlanmamış kesin kayıtlar, en yakın tarihli önce.
pub fn receivables(conn: &Connection, ctx: &Ctx) -> AppResult<Vec<ReservationSummary>> {
    ctx.user.require_admin()?;
    Ok(load_summaries(conn, "r.status = 'kesin' ORDER BY r.starts_at", &[], ctx.today())?
        .into_iter()
        .filter(|x| x.remaining > 0)
        .collect())
}

pub fn hall_usage(conn: &Connection, ctx: &Ctx, from: NaiveDate, to: NaiveDate) -> AppResult<Vec<HallUsageRow>> {
    ctx.user.require_admin()?;
    let mut stmt = conn.prepare(
        "SELECT h.name, COUNT(r.id), COALESCE(SUM(r.total), 0)
         FROM halls h LEFT JOIN reservations r ON r.hall_id = h.id AND r.status = 'kesin'
              AND r.starts_at >= ?1 AND r.starts_at < ?2
         GROUP BY h.id ORDER BY h.sort, h.name",
    )?;
    let end = midnight(to.succ_opt().unwrap_or(to));
    let rows = stmt.query_map(params![midnight(from), end], |r| {
        Ok(HallUsageRow { hall_name: r.get(0)?, event_count: r.get(1)?, sales: r.get(2)? })
    })?;
    Ok(rows.collect::<Result<_, _>>()?)
}
