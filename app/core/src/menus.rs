//! Yemek menüleri: katalogdaki menüler (yönetici düzenler) ve rezervasyona kopyalanan menü.
//!
//! Menünün kişi başı farkı (0 = paket fiyatına dahil) ücretlendirilen kişi sayısıyla çarpılıp
//! toplama eklenir. Satışta menünün adı, farkı ve yemekleri rezervasyona kopyalanır; böylece
//! katalogdaki menü sonradan değişse de yapılmış sözleşme ve o düğünün menüsü değişmez.

use rusqlite::{params, Connection, OptionalExtension, Transaction};

use crate::auth::audit;
use crate::error::{rule, AppError, AppResult};
use crate::model::*;
use crate::reservations;
use crate::util::{self, new_id, optional, parse_datetime, required};
use crate::Ctx;

const MAX_DISHES: usize = 80;

fn dishes(conn: &Connection, sql: &str, owner_id: &str) -> AppResult<Vec<MenuDish>> {
    let mut stmt = conn.prepare(sql)?;
    let rows = stmt.query_map([owner_id], |r| Ok(MenuDish { course: r.get(0)?, name: r.get(1)? }))?;
    Ok(rows.collect::<Result<_, _>>()?)
}

fn menu_items(conn: &Connection, menu_id: &str) -> AppResult<Vec<MenuDish>> {
    dishes(conn, "SELECT course, name FROM menu_items WHERE menu_id = ?1 ORDER BY ord", menu_id)
}

pub fn reservation_items(conn: &Connection, reservation_id: &str) -> AppResult<Vec<MenuDish>> {
    dishes(conn, "SELECT course, name FROM reservation_menu_items WHERE reservation_id = ?1 ORDER BY ord", reservation_id)
}

/// Yemek listesini denetler ve kırpar. Aynı bölümün yemekleri yazıldığı sırada kalır.
fn clean_dishes(items: &[MenuDish]) -> AppResult<Vec<MenuDish>> {
    if items.len() > MAX_DISHES {
        return rule(format!("Menüde en fazla {MAX_DISHES} yemek olabilir."));
    }
    items
        .iter()
        .map(|item| {
            let name = required(&item.name, "Yemek adı", 150)?;
            let course = required(&item.course, &format!("“{name}” yemeğinin bölümü (ör. Ana yemek)"), 60)?;
            Ok(MenuDish { course, name })
        })
        .collect()
}

// ---------------- Katalog ----------------
pub fn list(conn: &Connection, include_inactive: bool) -> AppResult<Vec<MenuDto>> {
    let filter = if include_inactive { "" } else { "WHERE is_active = 1" };
    let sql = format!(
        "SELECT id, name, description, per_guest_price, is_active FROM menus {filter}
         ORDER BY is_active DESC, per_guest_price, name"
    );
    let mut stmt = conn.prepare(&sql)?;
    let rows = stmt
        .query_map([], |r| {
            Ok(MenuDto {
                id: r.get(0)?,
                name: r.get(1)?,
                description: r.get(2)?,
                per_guest_price: r.get(3)?,
                is_active: r.get(4)?,
                items: Vec::new(),
            })
        })?
        .collect::<Result<Vec<_>, _>>()?;
    rows.into_iter()
        .map(|mut menu| {
            menu.items = menu_items(conn, &menu.id)?;
            Ok(menu)
        })
        .collect()
}

pub fn save(conn: &mut Connection, ctx: &Ctx, id: Option<&str>, request: &SaveMenuRequest) -> AppResult<String> {
    ctx.user.require_admin()?;
    let name = required(&request.name, "Menü adı", 150)?;
    if request.per_guest_price < 0 {
        return rule("Kişi başı fark eksi olamaz.");
    }
    let items = clean_dishes(&request.items)?;
    if items.is_empty() {
        return rule("Menüye en az bir yemek ekleyin.");
    }
    let description = optional(&request.description, 1000);
    let tx = conn.transaction()?;
    let id = match id {
        Some(id) => {
            let changed = tx.execute(
                "UPDATE menus SET name = ?1, description = ?2, per_guest_price = ?3, is_active = ?4 WHERE id = ?5",
                params![name, description, request.per_guest_price, request.is_active, id],
            )?;
            if changed == 0 {
                return rule("Menü bulunamadı.");
            }
            id.to_string()
        }
        None => {
            let id = new_id();
            tx.execute(
                "INSERT INTO menus (id, name, description, per_guest_price, is_active) VALUES (?1, ?2, ?3, ?4, ?5)",
                params![id, name, description, request.per_guest_price, request.is_active],
            )?;
            id
        }
    };
    tx.execute("DELETE FROM menu_items WHERE menu_id = ?1", [&id])?;
    for (ord, item) in items.iter().enumerate() {
        tx.execute(
            "INSERT INTO menu_items (id, menu_id, course, name, ord) VALUES (?1, ?2, ?3, ?4, ?5)",
            params![new_id(), id, item.course, item.name, ord as i64],
        )?;
    }
    audit(&tx, ctx, "menu_kaydedildi", &name)?;
    tx.commit()?;
    Ok(id)
}

// ---------------- Rezervasyondaki menü ----------------
/// Satışta / menü değişikliğinde rezervasyona kopyalanacak menü.
pub(crate) struct SaleMenu {
    pub name: String,
    pub per_guest_price: i64,
    pub items: Vec<MenuDish>,
}

pub(crate) fn load_for_sale(conn: &Connection, menu_id: &str) -> AppResult<SaleMenu> {
    let (name, per_guest_price) = conn
        .query_row("SELECT name, per_guest_price FROM menus WHERE id = ?1 AND is_active = 1", [menu_id], |r| {
            Ok((r.get::<_, String>(0)?, r.get::<_, i64>(1)?))
        })
        .optional()?
        .ok_or_else(|| AppError::Rule("Seçilen menü bulunamadı veya kullanımdan kaldırılmış.".into()))?;
    Ok(SaleMenu { name, per_guest_price, items: menu_items(conn, menu_id)? })
}

/// Menünün adını, farkını ve yemeklerini rezervasyona yazar (`None` menüyü kaldırır). Mutfak notlarına dokunmaz.
pub(crate) fn write_reservation_menu(
    tx: &Connection,
    reservation_id: &str,
    menu: Option<&SaleMenu>,
    menu_price: i64,
) -> AppResult<()> {
    tx.execute(
        "UPDATE reservations SET menu_name = ?1, menu_per_guest = ?2, menu_price = ?3 WHERE id = ?4",
        params![menu.map(|m| m.name.as_str()), menu.map_or(0, |m| m.per_guest_price), menu_price, reservation_id],
    )?;
    write_items(tx, reservation_id, menu.map_or(&[], |m| m.items.as_slice()))
}

fn write_items(tx: &Connection, reservation_id: &str, items: &[MenuDish]) -> AppResult<()> {
    tx.execute("DELETE FROM reservation_menu_items WHERE reservation_id = ?1", [reservation_id])?;
    for (ord, item) in items.iter().enumerate() {
        tx.execute(
            "INSERT INTO reservation_menu_items (id, reservation_id, course, name, ord) VALUES (?1, ?2, ?3, ?4, ?5)",
            params![new_id(), reservation_id, item.course, item.name, ord as i64],
        )?;
    }
    Ok(())
}

struct Booking {
    no: String,
    status: Status,
    event_date: chrono::NaiveDate,
    guests: i64,
    package_price: i64,
    min_guests: i64,
}

fn booking(tx: &Transaction<'_>, id: &str) -> AppResult<Booking> {
    let booking = tx
        .query_row(
            "SELECT no, status, starts_at, guest_count, package_price, package_min_guests FROM reservations WHERE id = ?1",
            [id],
            |r| {
                Ok(Booking {
                    no: r.get(0)?,
                    status: r.get(1)?,
                    event_date: parse_datetime(&r.get::<_, String>(2)?).date(),
                    guests: r.get(3)?,
                    package_price: r.get(4)?,
                    min_guests: r.get(5)?,
                })
            },
        )
        .optional()?
        .ok_or_else(|| AppError::NotFound("Rezervasyon bulunamadı.".into()))?;
    match booking.status {
        Status::Cancelled => rule("İptal edilmiş rezervasyonun menüsü değiştirilemez."),
        Status::Option => rule("Menü kesin kayıtta seçilir. Önce ön rezervasyonu kesin kayda çevirin."),
        Status::Confirmed => Ok(booking),
    }
}

/// Katalogdan menü seçer (veya kaldırır). Yemekler yeni menüyle değişir; fiyat farkı
/// toplam tutara ve ödenmemiş taksitlere yansır.
pub fn select(conn: &mut Connection, ctx: &Ctx, id: &str, request: &SelectMenuRequest) -> AppResult<()> {
    let tx = conn.transaction()?;
    let booking = booking(&tx, id)?;
    let menu = match &request.menu_id {
        Some(menu_id) => Some(load_for_sale(&tx, menu_id)?),
        None => None,
    };
    let menu_price = menu.as_ref().map_or(0, |m| menu_total(m.per_guest_price, booking.guests, booking.min_guests));
    let change = reservations::set_prices(
        &tx,
        id,
        booking.package_price,
        menu_price,
        None,
        "Ek ödeme (menü değişikliği)",
        "Önce iade yapın, sonra menüyü değiştirin.",
        ctx.today(),
        booking.event_date,
    )?;
    write_reservation_menu(&tx, id, menu.as_ref(), menu_price)?;
    let name = menu.as_ref().map_or("menü kaldırıldı", |m| m.name.as_str());
    let detail = match change {
        Some((old_total, new_total)) => {
            format!("{}: {name}, {} → {}", booking.no, util::money(old_total), util::money(new_total))
        }
        None => format!("{}: {name}", booking.no),
    };
    audit(&tx, ctx, "menu_secildi", detail)?;
    tx.commit()?;
    Ok(())
}

/// Bu düğüne özel yemekleri ve mutfak notlarını kaydeder. Fiyatı değiştirmez.
pub fn save_content(conn: &mut Connection, ctx: &Ctx, id: &str, request: &SaveReservationMenuRequest) -> AppResult<()> {
    let tx = conn.transaction()?;
    let booking = booking(&tx, id)?;
    let items = clean_dishes(&request.items)?;
    write_items(&tx, id, &items)?;
    tx.execute("UPDATE reservations SET menu_notes = ?1 WHERE id = ?2", params![optional(&request.notes, 2000), id])?;
    audit(&tx, ctx, "menu_icerigi_guncellendi", &booking.no)?;
    tx.commit()?;
    Ok(())
}

/// Kesin kayıtta ek hizmetleri ve seçenekleri (ör. pasta) değiştirir. Rezervasyonda zaten olan ek hizmet
/// satış anındaki fiyatıyla kalır; yeni eklenen katalogdaki güncel fiyatla gelir. Fark ödenmemiş taksitlere yansır.
pub fn change_extras(conn: &mut Connection, ctx: &Ctx, id: &str, request: &ChangeExtrasRequest) -> AppResult<()> {
    let tx = conn.transaction()?;
    let booking = booking(&tx, id)?;
    let (menu_price, current) = (
        tx.query_row("SELECT menu_price FROM reservations WHERE id = ?1", [id], |r| r.get::<_, i64>(0))?,
        reservations::extra_lines(&tx, id)?,
    );
    let mut extras: Vec<ExtraLine> = Vec::new();
    for extra_id in &request.extra_ids {
        if extras.iter().any(|x| x.extra_id.as_deref() == Some(extra_id.as_str())) {
            continue;
        }
        // Eski kayıtlarda katalog bağlantısı yoktur; aynı adlı satır o ek hizmet sayılır.
        let catalog_name: Option<String> =
            tx.query_row("SELECT name FROM extras WHERE id = ?1", [extra_id], |r| r.get(0)).optional()?;
        let kept = current.iter().find(|x| {
            x.extra_id.as_deref() == Some(extra_id.as_str())
                || (x.extra_id.is_none() && catalog_name.as_deref() == Some(x.name.as_str()))
        });
        let line = match kept {
            Some(line) => ExtraLine { extra_id: Some(extra_id.clone()), ..line.clone() },
            None => reservations::catalog_extra(&tx, extra_id, booking.guests, booking.min_guests)?,
        };
        extras.push(line);
    }
    reservations::check_groups(&extras)?;
    let change = reservations::set_prices(
        &tx,
        id,
        booking.package_price,
        menu_price,
        Some(extras.iter().map(|x| x.price).sum()),
        "Ek ödeme (seçenek değişikliği)",
        "Önce iade yapın, sonra seçenekleri değiştirin.",
        ctx.today(),
        booking.event_date,
    )?;
    reservations::write_extras(&tx, id, &extras)?;
    let names = extras.iter().map(|x| x.name.as_str()).collect::<Vec<_>>().join(", ");
    let detail = match change {
        Some((old_total, new_total)) => {
            format!("{}: {names}, {} → {}", booking.no, util::money(old_total), util::money(new_total))
        }
        None => format!("{}: {names}", booking.no),
    };
    audit(&tx, ctx, "ek_hizmetler_degisti", detail)?;
    tx.commit()?;
    Ok(())
}
