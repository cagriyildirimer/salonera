use chrono::{Datelike, Duration, NaiveDate, NaiveDateTime};
use rusqlite::{params, params_from_iter, Connection, OptionalExtension, Transaction};

use crate::auth::audit;
use crate::customers;
use crate::menus;
use crate::error::{rule, AppError, AppResult};
use crate::model::*;
use crate::payments;
use crate::util::{self, event_range, fmt_date, fmt_datetime, new_id, optional, parse_date, parse_datetime};
use crate::Ctx;

const SUMMARY_SELECT: &str = "
SELECT r.id, r.no, r.starts_at, r.ends_at, r.hall_id, h.name, r.customer_id, c.full_name, c.phone,
       r.kind, r.guest_count, r.status, r.option_until, r.total,
       COALESCE((SELECT SUM(CASE WHEN p.is_refund = 1 THEN -p.amount ELSE p.amount END)
                 FROM payments p WHERE p.reservation_id = r.id AND p.is_cancelled = 0), 0)
FROM reservations r
JOIN customers c ON c.id = r.customer_id
JOIN halls h ON h.id = r.hall_id";

/// Ödemeleri taksitlere sırayla dağıtır: önce ilk taksit kapanır, artan para sonrakine geçer.
/// Böylece ödeme planı değişse bile hangi taksidin ödendiği her zaman doğru hesaplanır.
pub fn allocate(installments: &[(String, String, NaiveDate, i64)], paid: i64, today: NaiveDate) -> Vec<InstallmentDto> {
    let mut left = paid.max(0);
    installments
        .iter()
        .map(|(id, label, due, amount)| {
            let paid_here = left.min(*amount);
            left -= paid_here;
            let state = if paid_here >= *amount {
                InstallmentState::Paid
            } else if *due < today {
                InstallmentState::Late
            } else if paid_here > 0 {
                InstallmentState::Partial
            } else {
                InstallmentState::Waiting
            };
            InstallmentDto { id: id.clone(), label: label.clone(), due_date: *due, amount: *amount, paid: paid_here, state }
        })
        .collect()
}

pub(crate) fn installment_rows(conn: &Connection, reservation_id: &str) -> AppResult<Vec<(String, String, NaiveDate, i64)>> {
    let mut stmt =
        conn.prepare("SELECT id, label, due_date, amount FROM installments WHERE reservation_id = ?1 ORDER BY ord")?;
    let rows = stmt.query_map([reservation_id], |r| {
        Ok((r.get(0)?, r.get(1)?, parse_date(&r.get::<_, String>(2)?), r.get(3)?))
    })?;
    Ok(rows.collect::<Result<_, _>>()?)
}

/// Rezervasyon listesini filtreyle yükler. `filter` yalnızca bu dosyadaki sabit SQL parçalarıdır.
pub fn load_summaries(
    conn: &Connection,
    filter: &str,
    args: &[&dyn rusqlite::ToSql],
    today: NaiveDate,
) -> AppResult<Vec<ReservationSummary>> {
    let sql = format!("{SUMMARY_SELECT} WHERE {filter}");
    let mut stmt = conn.prepare(&sql)?;
    let rows = stmt.query_map(params_from_iter(args.iter()), |r| {
        let starts = parse_datetime(&r.get::<_, String>(2)?);
        let ends = parse_datetime(&r.get::<_, String>(3)?);
        let status: Status = r.get(11)?;
        let option_until: Option<NaiveDate> = r.get::<_, Option<String>>(12)?.map(|v| parse_date(&v));
        let total: i64 = r.get(13)?;
        let paid: i64 = r.get(14)?;
        Ok(ReservationSummary {
            id: r.get(0)?,
            no: r.get(1)?,
            date: starts.date(),
            start: starts.format("%H:%M").to_string(),
            end: ends.format("%H:%M").to_string(),
            hall_id: r.get(4)?,
            hall_name: r.get(5)?,
            customer_id: r.get(6)?,
            customer_name: r.get(7)?,
            customer_phone: r.get(8)?,
            kind: r.get(9)?,
            guest_count: r.get(10)?,
            status,
            option_until,
            option_expired: status == Status::Option && option_until.is_some_and(|d| d < today),
            total,
            paid,
            remaining: if status == Status::Confirmed { (total - paid).max(0) } else { 0 },
            overdue: 0,
        })
    })?;
    let mut list: Vec<ReservationSummary> = rows.collect::<Result<_, _>>()?;
    for item in list.iter_mut().filter(|x| x.status == Status::Confirmed && x.remaining > 0) {
        let plan = installment_rows(conn, &item.id)?;
        item.overdue = allocate(&plan, item.paid, today)
            .iter()
            .filter(|x| x.state == InstallmentState::Late)
            .map(|x| x.amount - x.paid)
            .sum();
    }
    Ok(list)
}

pub fn summary(conn: &Connection, id: &str, today: NaiveDate) -> AppResult<ReservationSummary> {
    load_summaries(conn, "r.id = ?1", &[&id], today)?
        .into_iter()
        .next()
        .ok_or_else(|| AppError::NotFound("Rezervasyon bulunamadı.".into()))
}

/// Takvim: verilen tarih aralığındaki (bitiş hariç) organizasyonlar. İptaller gösterilmez.
pub fn calendar(conn: &Connection, from: NaiveDate, to: NaiveDate, today: NaiveDate) -> AppResult<Vec<ReservationSummary>> {
    let from = fmt_datetime(from.and_hms_opt(0, 0, 0).unwrap());
    let to = fmt_datetime(to.and_hms_opt(0, 0, 0).unwrap());
    load_summaries(
        conn,
        "r.status <> 'iptal' AND r.starts_at < ?2 AND r.ends_at > ?1 ORDER BY r.starts_at, h.sort",
        &[&from, &to],
        today,
    )
}

/// Müşteri adı, telefon veya rezervasyon numarasıyla arama (iptaller dahil, en yeni önce).
pub fn search(conn: &Connection, query: &str, today: NaiveDate) -> AppResult<Vec<ReservationSummary>> {
    let text = format!("%{}%", util::turkish_lower(query));
    let digits = util::digits(query);
    let phone = if digits.len() >= 3 { format!("%{digits}%") } else { "\u{0}".to_string() };
    load_summaries(
        conn,
        "(c.search_name LIKE ?1 OR r.no LIKE ?1 OR REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(c.phone, ' ', ''), '-', ''), '(', ''), ')', ''), '.', '') LIKE ?2)
         ORDER BY r.starts_at DESC LIMIT 100",
        &[&text, &phone],
        today,
    )
}

/// Aynı salonda çakışan geçerli bir kayıt var mı? Süresi dolmuş ön rezervasyonlar ve iptaller engel değildir.
fn find_conflict(
    conn: &Connection,
    hall_id: &str,
    starts: NaiveDateTime,
    ends: NaiveDateTime,
    exclude: Option<&str>,
    today: NaiveDate,
) -> AppResult<Option<String>> {
    Ok(conn
        .query_row(
            "SELECT c.full_name, r.starts_at, r.ends_at, r.status FROM reservations r
             JOIN customers c ON c.id = r.customer_id
             WHERE r.hall_id = ?1 AND r.status <> 'iptal'
               AND NOT (r.status = 'on_rezervasyon' AND r.option_until < ?4)
               AND r.starts_at < ?3 AND r.ends_at > ?2 AND r.id <> ?5
             ORDER BY r.starts_at LIMIT 1",
            params![hall_id, fmt_datetime(starts), fmt_datetime(ends), fmt_date(today), exclude.unwrap_or("")],
            |r| {
                let s = parse_datetime(&r.get::<_, String>(1)?);
                let e = parse_datetime(&r.get::<_, String>(2)?);
                let status: Status = r.get(3)?;
                let kind = if status == Status::Option { " – ön rezervasyon" } else { "" };
                Ok(format!("{} ({}–{}{kind})", r.get::<_, String>(0)?, s.format("%H:%M"), e.format("%H:%M")))
            },
        )
        .optional()?)
}

pub fn availability(
    conn: &Connection,
    date: NaiveDate,
    start: &str,
    end: &str,
    exclude: Option<&str>,
    today: NaiveDate,
) -> AppResult<Vec<HallAvailability>> {
    let (starts, ends) = event_range(date, start, end)?;
    let halls = crate::catalog::list_halls(conn, false)?;
    halls
        .into_iter()
        .map(|hall| {
            let busy = find_conflict(conn, &hall.id, starts, ends, exclude, today)?;
            Ok(HallAvailability { hall_id: hall.id, hall_name: hall.name, capacity: hall.capacity, is_free: busy.is_none(), busy_with: busy })
        })
        .collect()
}

struct Hall {
    name: String,
    capacity: i64,
}

fn active_hall(conn: &Connection, id: &str) -> AppResult<Hall> {
    conn.query_row("SELECT name, capacity FROM halls WHERE id = ?1 AND is_active = 1", [id], |r| {
        Ok(Hall { name: r.get(0)?, capacity: r.get(1)? })
    })
    .optional()?
    .ok_or_else(|| AppError::Rule("Seçilen salon bulunamadı veya kullanımda değil.".into()))
}

fn validate_event(
    conn: &Connection,
    hall_id: &str,
    date: NaiveDate,
    start: &str,
    end: &str,
    guests: i64,
    exclude: Option<&str>,
    today: NaiveDate,
) -> AppResult<(NaiveDateTime, NaiveDateTime)> {
    let hall = active_hall(conn, hall_id)?;
    let (starts, ends) = event_range(date, start, end)?;
    if date < today {
        return rule("Geçmiş bir tarihe kayıt yapılamaz.");
    }
    if guests <= 0 {
        return rule("Davetli sayısını yazın.");
    }
    if guests > hall.capacity {
        return rule(format!(
            "{} en fazla {} kişi alıyor. Davetli sayısını azaltın veya başka bir salon seçin.",
            hall.name, hall.capacity
        ));
    }
    if let Some(busy) = find_conflict(conn, hall_id, starts, ends, exclude, today)? {
        return rule(format!("{} bu saatlerde dolu: {busy}. Başka bir saat veya salon seçin.", hall.name));
    }
    Ok((starts, ends))
}

fn next_number(tx: &Transaction<'_>, year: i32) -> AppResult<String> {
    let count: i64 =
        tx.query_row("SELECT COUNT(*) FROM reservations WHERE no LIKE ?1", [format!("{year}-%")], |r| r.get(0))?;
    Ok(format!("{year}-{:04}", count + 1))
}

fn option_until(today: NaiveDate, event_date: NaiveDate, days: i64) -> AppResult<NaiveDate> {
    if !(1..=90).contains(&days) {
        return rule("Ön rezervasyon süresi 1 ile 90 gün arasında olmalıdır.");
    }
    Ok((today + Duration::days(days)).min(event_date))
}

pub fn create(conn: &mut Connection, ctx: &Ctx, request: &CreateReservationRequest) -> AppResult<CreateReservationResponse> {
    let today = ctx.today();
    let tx = conn.transaction()?;
    let (starts, ends) = validate_event(
        &tx,
        &request.hall_id,
        request.date,
        &request.start,
        &request.end,
        request.guest_count,
        None,
        today,
    )?;
    let (status, until) = match (&request.sale, request.option_days) {
        (Some(_), Some(_)) => return rule("Kayıt ya ön rezervasyon ya kesin kayıt olabilir, ikisi birden olamaz."),
        (None, None) => return rule("Kayıt türünü seçin: ön rezervasyon veya kesin kayıt."),
        (Some(_), None) => (Status::Confirmed, None),
        (None, Some(days)) => (Status::Option, Some(option_until(today, request.date, days)?)),
    };
    let customer_id = match (&request.customer_id, &request.new_customer) {
        (Some(id), _) => {
            customers::ensure_exists(&tx, id)?;
            id.clone()
        }
        (None, Some(new_customer)) => customers::insert(&tx, ctx, new_customer)?,
        (None, None) => return rule("Müşteri seçin veya yeni müşteri bilgilerini yazın."),
    };
    let id = new_id();
    let no = next_number(&tx, ctx.now.year())?;
    tx.execute(
        "INSERT INTO reservations (id, no, customer_id, hall_id, starts_at, ends_at, kind, guest_count, status,
                                   option_until, notes, created_by, created_at)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, 'on_rezervasyon', ?9, ?10, ?11, ?12)",
        params![
            id,
            no,
            customer_id,
            request.hall_id,
            fmt_datetime(starts),
            fmt_datetime(ends),
            request.kind,
            request.guest_count,
            until.map(fmt_date),
            optional(&request.notes, 2000),
            ctx.user.id,
            fmt_datetime(ctx.now)
        ],
    )?;
    let mut payment_id = None;
    if let (Status::Confirmed, Some(sale)) = (status, &request.sale) {
        payment_id = apply_sale(&tx, ctx, &id, request.date, sale)?;
    }
    audit(&tx, ctx, "rezervasyon_olusturuldu", &no)?;
    tx.commit()?;
    Ok(CreateReservationResponse { reservation_id: id, no, payment_id })
}

/// Paketi ve fiyatı yazar, ödeme planını kurar, varsa kaporayı alır ve kaydı kesinleştirir.
fn apply_sale(
    tx: &Transaction<'_>,
    ctx: &Ctx,
    reservation_id: &str,
    event_date: NaiveDate,
    sale: &SaleInput,
) -> AppResult<Option<String>> {
    let (package_name, pricing): (String, PackagePricing) = tx
        .query_row(
            "SELECT name, price, per_guest_price, min_guests FROM packages WHERE id = ?1 AND is_active = 1",
            [&sale.package_id],
            |r| {
                Ok((r.get(0)?, PackagePricing { base_price: r.get(1)?, per_guest_price: r.get(2)?, min_guests: r.get(3)? }))
            },
        )
        .optional()?
        .ok_or_else(|| AppError::Rule("Seçilen paket bulunamadı veya satıştan kaldırılmış.".into()))?;
    let guests: i64 = tx.query_row("SELECT guest_count FROM reservations WHERE id = ?1", [reservation_id], |r| r.get(0))?;
    let package_price = pricing.total(guests);
    let mut extras = Vec::new();
    for extra_id in &sale.extra_ids {
        if !extras.iter().any(|x: &ExtraLine| x.extra_id.as_deref() == Some(extra_id.as_str())) {
            extras.push(catalog_extra(tx, extra_id, guests, pricing.min_guests)?);
        }
    }
    check_groups(&extras)?;
    let menu = match &sale.menu_id {
        Some(menu_id) => Some(menus::load_for_sale(tx, menu_id)?),
        None => None,
    };
    let menu_price = menu.as_ref().map_or(0, |m| menu_total(m.per_guest_price, guests, pricing.min_guests));
    let gross = package_price + menu_price + extras.iter().map(|x| x.price).sum::<i64>();
    if sale.discount < 0 {
        return rule("İndirim eksi olamaz.");
    }
    if sale.discount > gross {
        return rule(format!("İndirim, toplam fiyattan ({}) büyük olamaz.", util::money(gross)));
    }
    let total = gross - sale.discount;

    write_extras(tx, reservation_id, &extras)?;
    tx.execute(
        "UPDATE reservations SET status = 'kesin', option_until = NULL, package_name = ?1, package_price = ?2,
                discount = ?3, total = ?4, confirmed_at = ?5, package_base_price = ?6, package_per_guest = ?7,
                package_min_guests = ?8 WHERE id = ?9",
        params![
            package_name,
            package_price,
            sale.discount,
            total,
            fmt_datetime(ctx.now),
            pricing.base_price,
            pricing.per_guest_price,
            pricing.min_guests,
            reservation_id
        ],
    )?;
    menus::write_reservation_menu(tx, reservation_id, menu.as_ref(), menu_price)?;

    let today = ctx.today();
    let plan = if sale.plan.is_empty() {
        default_plan(total, sale.first_payment.as_ref().map_or(0, |p| p.amount), today, event_date)
    } else {
        sale.plan.clone()
    };
    write_plan(tx, reservation_id, total, &plan)?;

    match &sale.first_payment {
        Some(payment) if payment.amount > 0 => Ok(Some(payments::insert(
            tx,
            ctx,
            reservation_id,
            payment.amount,
            payment.method,
            payment.note.as_deref(),
            false,
        )?)),
        _ => Ok(None),
    }
}

/// Plan verilmezse: varsa kapora bugüne, kalanı düğünden bir hafta önceye.
pub fn default_plan(total: i64, deposit: i64, today: NaiveDate, event_date: NaiveDate) -> Vec<InstallmentInput> {
    let mut plan = Vec::new();
    let deposit = deposit.clamp(0, total);
    if deposit > 0 {
        plan.push(InstallmentInput { label: "Kapora".into(), due_date: today, amount: deposit });
    }
    if total - deposit > 0 {
        let due = (event_date - Duration::days(7)).max(today);
        plan.push(InstallmentInput { label: "Kalan ödeme".into(), due_date: due, amount: total - deposit });
    }
    plan
}

fn write_plan(tx: &Connection, reservation_id: &str, total: i64, plan: &[InstallmentInput]) -> AppResult<()> {
    let sum: i64 = plan.iter().map(|x| x.amount).sum();
    if plan.iter().any(|x| x.amount <= 0) {
        return rule("Ödeme planındaki her tutar sıfırdan büyük olmalıdır.");
    }
    if total > 0 && sum != total {
        return rule(format!(
            "Ödeme planının toplamı ({}) sözleşme tutarına ({}) eşit olmalıdır.",
            util::money(sum),
            util::money(total)
        ));
    }
    if plan.len() > 36 {
        return rule("Ödeme planında en fazla 36 taksit olabilir.");
    }
    tx.execute("DELETE FROM installments WHERE reservation_id = ?1", [reservation_id])?;
    let mut sorted = plan.to_vec();
    sorted.sort_by_key(|x| x.due_date);
    for (index, item) in sorted.iter().enumerate() {
        let label = util::required(&item.label, "Taksit adı", 60)?;
        tx.execute(
            "INSERT INTO installments (id, reservation_id, label, due_date, amount, ord) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![new_id(), reservation_id, label, fmt_date(item.due_date), item.amount, index as i64],
        )?;
    }
    Ok(())
}

struct Current {
    no: String,
    status: Status,
    starts: NaiveDateTime,
    hall_id: String,
}

fn current(conn: &Connection, id: &str) -> AppResult<Current> {
    conn.query_row("SELECT no, status, starts_at, hall_id FROM reservations WHERE id = ?1", [id], |r| {
        Ok(Current { no: r.get(0)?, status: r.get(1)?, starts: parse_datetime(&r.get::<_, String>(2)?), hall_id: r.get(3)? })
    })
    .optional()?
    .ok_or_else(|| AppError::NotFound("Rezervasyon bulunamadı.".into()))
}

/// Ön rezervasyonu kesin kayda çevirir. Süresi dolmuşsa tarih hâlâ boşsa yapılabilir.
pub fn confirm(conn: &mut Connection, ctx: &Ctx, id: &str, sale: &SaleInput) -> AppResult<Option<String>> {
    let tx = conn.transaction()?;
    let existing = current(&tx, id)?;
    if existing.status != Status::Option {
        return rule("Yalnızca ön rezervasyonlar kesin kayda çevrilebilir.");
    }
    let ends: String = tx.query_row("SELECT ends_at FROM reservations WHERE id = ?1", [id], |r| r.get(0))?;
    if let Some(busy) = find_conflict(&tx, &existing.hall_id, existing.starts, parse_datetime(&ends), Some(id), ctx.today())? {
        return rule(format!("Ön rezervasyonun süresi dolduğu için bu saat başkasına verilmiş: {busy}."));
    }
    let payment = apply_sale(&tx, ctx, id, existing.starts.date(), sale)?;
    audit(&tx, ctx, "rezervasyon_kesinlesti", &existing.no)?;
    tx.commit()?;
    Ok(payment)
}

pub fn update(conn: &mut Connection, ctx: &Ctx, id: &str, request: &UpdateReservationRequest) -> AppResult<()> {
    let tx = conn.transaction()?;
    let existing = current(&tx, id)?;
    if existing.status == Status::Cancelled {
        return rule("İptal edilmiş rezervasyon değiştirilemez.");
    }
    let old_guests: i64 = tx.query_row("SELECT guest_count FROM reservations WHERE id = ?1", [id], |r| r.get(0))?;
    // Geçmiş tarihli bir kaydın yalnızca notu/davetli sayısı düzeltiliyorsa tarih kontrolüne takılmasın.
    let today = if request.date == existing.starts.date() { request.date.min(ctx.today()) } else { ctx.today() };
    let (starts, ends) = validate_event(
        &tx,
        &request.hall_id,
        request.date,
        &request.start,
        &request.end,
        request.guest_count,
        Some(id),
        today,
    )?;
    tx.execute(
        "UPDATE reservations SET hall_id = ?1, starts_at = ?2, ends_at = ?3, kind = ?4, guest_count = ?5, notes = ?6
         WHERE id = ?7",
        params![
            request.hall_id,
            fmt_datetime(starts),
            fmt_datetime(ends),
            request.kind,
            request.guest_count,
            optional(&request.notes, 2000),
            id
        ],
    )?;
    audit(&tx, ctx, "rezervasyon_degisti", &existing.no)?;
    if request.reprice && existing.status == Status::Confirmed && request.guest_count != old_guests {
        if let Some((old_total, new_total)) = reprice(&tx, id, request.guest_count, ctx.today(), starts.date())? {
            audit(
                &tx,
                ctx,
                "fiyat_kisi_sayisina_gore_guncellendi",
                format!(
                    "{}: {} → {} kişi, {} → {}",
                    existing.no,
                    old_guests,
                    request.guest_count,
                    util::money(old_total),
                    util::money(new_total)
                ),
            )?;
        }
    }
    tx.commit()?;
    Ok(())
}

/// Kesin kayıtta paket tutarını ve menü farkını yeni davetli sayısıyla, satış anındaki fiyatlarla
/// yeniden hesaplar; toplamı ve ödeme planını buna göre düzeltir. Fiyat değişmiyorsa hiçbir şey yapmaz.
/// Değiştiyse (eski toplam, yeni toplam) döner.
fn reprice(
    tx: &Transaction<'_>,
    id: &str,
    guests: i64,
    today: NaiveDate,
    event_date: NaiveDate,
) -> AppResult<Option<(i64, i64)>> {
    let (pricing, old_package, menu_per_guest) = tx.query_row(
        "SELECT package_base_price, package_per_guest, package_min_guests, package_price, menu_per_guest
         FROM reservations WHERE id = ?1",
        [id],
        |r| {
            Ok((
                PackagePricing { base_price: r.get(0)?, per_guest_price: r.get(1)?, min_guests: r.get(2)? },
                r.get::<_, i64>(3)?,
                r.get::<_, i64>(4)?,
            ))
        },
    )?;
    // Kişi başı fiyatı olmayan paketin tutarı kişi sayısından bağımsızdır; olduğu gibi kalır.
    let new_package = if pricing.per_guest_price == 0 { old_package } else { pricing.total(guests) };
    let new_menu = menu_total(menu_per_guest, guests, pricing.min_guests);
    let mut extras = extra_lines(tx, id)?;
    for extra in &mut extras {
        extra.price = extra.priced_for(guests, pricing.min_guests);
    }
    let change = set_prices(
        tx,
        id,
        new_package,
        new_menu,
        Some(extras.iter().map(|x| x.price).sum()),
        "Ek ödeme (davetli artışı)",
        "Önce iade yapın, sonra davetli sayısını değiştirin.",
        today,
        event_date,
    )?;
    if change.is_some() {
        write_extras(tx, id, &extras)?;
    }
    Ok(change)
}

/// Katalogdaki ek hizmeti satış fiyatıyla getirir (kişi başı kısım verilen kişi sayısıyla hesaplanır).
pub(crate) fn catalog_extra(tx: &Connection, extra_id: &str, guests: i64, min_guests: i64) -> AppResult<ExtraLine> {
    let mut line = tx
        .query_row(
            "SELECT name, group_name, price, per_guest_price FROM extras WHERE id = ?1 AND is_active = 1",
            [extra_id],
            |r| {
                Ok(ExtraLine {
                    extra_id: Some(extra_id.to_string()),
                    name: r.get(0)?,
                    group_name: r.get(1)?,
                    price: 0,
                    base_price: r.get(2)?,
                    per_guest: r.get(3)?,
                })
            },
        )
        .optional()?
        .ok_or_else(|| AppError::Rule("Seçilen ek hizmetlerden biri bulunamadı veya satıştan kaldırılmış.".into()))?;
    line.price = line.base_price + menu_total(line.per_guest, guests, min_guests);
    Ok(line)
}

/// Aynı seçenek grubundan (ör. "Pasta") yalnızca biri seçilebilir.
pub(crate) fn check_groups(extras: &[ExtraLine]) -> AppResult<()> {
    for (index, extra) in extras.iter().enumerate() {
        if let Some(group) = &extra.group_name {
            if extras[..index].iter().any(|x| x.group_name.as_ref() == Some(group)) {
                return rule(format!("“{group}” seçeneklerinden yalnızca biri seçilebilir."));
            }
        }
    }
    Ok(())
}

pub(crate) fn extra_lines(conn: &Connection, reservation_id: &str) -> AppResult<Vec<ExtraLine>> {
    let mut stmt = conn.prepare(
        "SELECT extra_id, name, group_name, price, base_price, per_guest FROM reservation_extras
         WHERE reservation_id = ?1 ORDER BY group_name IS NULL, group_name, name",
    )?;
    let rows = stmt.query_map([reservation_id], |r| {
        Ok(ExtraLine {
            extra_id: r.get(0)?,
            name: r.get(1)?,
            group_name: r.get(2)?,
            price: r.get(3)?,
            base_price: r.get(4)?,
            per_guest: r.get(5)?,
        })
    })?;
    Ok(rows.collect::<Result<_, _>>()?)
}

pub(crate) fn write_extras(tx: &Connection, reservation_id: &str, extras: &[ExtraLine]) -> AppResult<()> {
    tx.execute("DELETE FROM reservation_extras WHERE reservation_id = ?1", [reservation_id])?;
    for extra in extras {
        tx.execute(
            "INSERT INTO reservation_extras (id, reservation_id, extra_id, name, group_name, price, base_price, per_guest)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)",
            params![
                new_id(),
                reservation_id,
                extra.extra_id,
                extra.name,
                extra.group_name,
                extra.price,
                extra.base_price,
                extra.per_guest
            ],
        )?;
    }
    Ok(())
}

/// Paket tutarını, menü farkını ve (verildiyse) ek hizmetlerin toplamını yeni değerlere çeker; toplamı ve
/// ödenmemiş taksitleri düzeltir. Ek hizmet satırlarını çağıran yazar. Hiçbiri değişmiyorsa hiçbir şey yapmaz.
/// Değiştiyse (eski toplam, yeni toplam) döner.
pub(crate) fn set_prices(
    tx: &Transaction<'_>,
    id: &str,
    new_package: i64,
    new_menu: i64,
    new_extras: Option<i64>,
    extra_label: &str,
    refund_hint: &str,
    today: NaiveDate,
    event_date: NaiveDate,
) -> AppResult<Option<(i64, i64)>> {
    let (old_package, old_menu, discount, old_total) = tx.query_row(
        "SELECT package_price, menu_price, discount, total FROM reservations WHERE id = ?1",
        [id],
        |r| Ok((r.get::<_, i64>(0)?, r.get::<_, i64>(1)?, r.get::<_, i64>(2)?, r.get::<_, i64>(3)?)),
    )?;
    let old_extras: i64 =
        tx.query_row("SELECT COALESCE(SUM(price), 0) FROM reservation_extras WHERE reservation_id = ?1", [id], |r| r.get(0))?;
    let extras = new_extras.unwrap_or(old_extras);
    if new_package == old_package && new_menu == old_menu && extras == old_extras {
        return Ok(None);
    }
    let gross = new_package + new_menu + extras;
    if discount > gross {
        return rule(format!(
            "Yapılmış indirim ({}) yeni tutardan ({}) büyük kalıyor. Fiyat güncellenemedi.",
            util::money(discount),
            util::money(gross)
        ));
    }
    let new_total = gross - discount;
    let paid = payments::net_paid(tx, id)?;
    if new_total < paid {
        return rule(format!(
            "Müşteri şimdiye kadar {} ödedi; yeni tutar ({}) bundan az olamaz. {refund_hint}",
            util::money(paid),
            util::money(new_total)
        ));
    }
    let plan = adjust_plan(&installment_rows(tx, id)?, paid, new_total - old_total, extra_label, today, event_date);
    tx.execute(
        "UPDATE reservations SET package_price = ?1, menu_price = ?2, total = ?3 WHERE id = ?4",
        params![new_package, new_menu, new_total, id],
    )?;
    write_plan(tx, id, new_total, &plan)?;
    Ok(Some((old_total, new_total)))
}

/// Toplam `delta` kadar değiştiğinde ödeme planını düzeltir. Ödenmiş kısımlara dokunulmaz:
/// artış ödenmemiş son taksite eklenir (hepsi ödenmişse `extra_label` adıyla yeni bir taksit açılır);
/// azalış sondan başlayarak ödenmemiş tutarlardan düşülür, sıfırlanan taksit kaldırılır.
pub fn adjust_plan(
    rows: &[(String, String, NaiveDate, i64)],
    paid: i64,
    delta: i64,
    extra_label: &str,
    today: NaiveDate,
    event_date: NaiveDate,
) -> Vec<InstallmentInput> {
    let allocated = allocate(rows, paid, today);
    let mut plan: Vec<(InstallmentInput, i64)> = allocated
        .iter()
        .map(|x| (InstallmentInput { label: x.label.clone(), due_date: x.due_date, amount: x.amount }, x.paid))
        .collect();
    if delta > 0 {
        match plan.iter_mut().rev().find(|(item, paid_here)| *paid_here < item.amount) {
            Some((item, _)) => item.amount += delta,
            None => {
                let last_due = plan.last().map_or(today, |(item, _)| item.due_date);
                let due = (event_date - Duration::days(7)).max(today).max(last_due);
                plan.push((InstallmentInput { label: extra_label.into(), due_date: due, amount: delta }, 0));
            }
        }
    } else if delta < 0 {
        let mut left = -delta;
        for (item, paid_here) in plan.iter_mut().rev() {
            let cut = left.min(item.amount - *paid_here);
            item.amount -= cut;
            left -= cut;
            if left == 0 {
                break;
            }
        }
        plan.retain(|(item, _)| item.amount > 0);
    }
    plan.into_iter().map(|(item, _)| item).collect()
}

pub fn cancel(conn: &mut Connection, ctx: &Ctx, id: &str, reason: &str) -> AppResult<()> {
    let reason = util::required(reason, "İptal nedeni", 500)?;
    let tx = conn.transaction()?;
    let existing = current(&tx, id)?;
    if existing.status == Status::Cancelled {
        return rule("Bu rezervasyon zaten iptal edilmiş.");
    }
    tx.execute(
        "UPDATE reservations SET status = 'iptal', cancel_reason = ?1, option_until = NULL WHERE id = ?2",
        params![reason, id],
    )?;
    audit(&tx, ctx, "rezervasyon_iptal", format!("{}: {reason}", existing.no))?;
    tx.commit()?;
    Ok(())
}

pub fn extend_option(conn: &mut Connection, ctx: &Ctx, id: &str, days: i64) -> AppResult<()> {
    let tx = conn.transaction()?;
    let existing = current(&tx, id)?;
    if existing.status != Status::Option {
        return rule("Süre yalnızca ön rezervasyonlarda uzatılabilir.");
    }
    let ends: String = tx.query_row("SELECT ends_at FROM reservations WHERE id = ?1", [id], |r| r.get(0))?;
    if let Some(busy) = find_conflict(&tx, &existing.hall_id, existing.starts, parse_datetime(&ends), Some(id), ctx.today())? {
        return rule(format!("Süre dolduğu için bu saat başkasına verilmiş: {busy}."));
    }
    let until = option_until(ctx.today(), existing.starts.date(), days)?;
    tx.execute("UPDATE reservations SET option_until = ?1 WHERE id = ?2", params![fmt_date(until), id])?;
    audit(&tx, ctx, "on_rezervasyon_uzatildi", &existing.no)?;
    tx.commit()?;
    Ok(())
}

pub fn set_plan(conn: &mut Connection, ctx: &Ctx, id: &str, plan: &[InstallmentInput]) -> AppResult<()> {
    let tx = conn.transaction()?;
    let existing = current(&tx, id)?;
    if existing.status != Status::Confirmed {
        return rule("Ödeme planı yalnızca kesin kayıtlarda düzenlenebilir.");
    }
    let total: i64 = tx.query_row("SELECT total FROM reservations WHERE id = ?1", [id], |r| r.get(0))?;
    if plan.is_empty() && total > 0 {
        return rule("Ödeme planına en az bir ödeme ekleyin.");
    }
    write_plan(&tx, id, total, plan)?;
    audit(&tx, ctx, "odeme_plani_degisti", &existing.no)?;
    tx.commit()?;
    Ok(())
}

pub fn detail(conn: &Connection, id: &str, today: NaiveDate) -> AppResult<ReservationDetail> {
    let summary = summary(conn, id, today)?;
    let row = conn.query_row(
        "SELECT r.package_name, r.package_price, r.discount, r.notes, r.cancel_reason, u.name, r.created_at, r.confirmed_at,
                r.package_base_price, r.package_per_guest, r.package_min_guests,
                r.menu_name, r.menu_per_guest, r.menu_price, r.menu_notes
         FROM reservations r JOIN users u ON u.id = r.created_by WHERE r.id = ?1",
        [id],
        |r| {
            Ok((
                r.get::<_, Option<String>>(0)?,
                r.get::<_, i64>(1)?,
                r.get::<_, i64>(2)?,
                r.get::<_, Option<String>>(3)?,
                r.get::<_, Option<String>>(4)?,
                r.get::<_, String>(5)?,
                r.get::<_, String>(6)?,
                r.get::<_, Option<String>>(7)?,
                PackagePricing { base_price: r.get(8)?, per_guest_price: r.get(9)?, min_guests: r.get(10)? },
                (
                    r.get::<_, Option<String>>(11)?,
                    r.get::<_, i64>(12)?,
                    r.get::<_, i64>(13)?,
                    r.get::<_, Option<String>>(14)?,
                ),
            ))
        },
    )?;
    let extras = extra_lines(conn, id)?;
    let installments = allocate(&installment_rows(conn, id)?, summary.paid, today);
    let payments = payments::list_for_reservation(conn, id)?;
    Ok(ReservationDetail {
        summary,
        package_name: row.0,
        package_price: row.1,
        package_pricing: row.8,
        extras,
        menu_name: row.9 .0,
        menu_per_guest: row.9 .1,
        menu_price: row.9 .2,
        menu_items: menus::reservation_items(conn, id)?,
        menu_notes: row.9 .3,
        discount: row.2,
        notes: row.3,
        cancel_reason: row.4,
        installments,
        payments,
        created_by: row.5,
        created_at: row.6,
        confirmed_at: row.7,
    })
}
