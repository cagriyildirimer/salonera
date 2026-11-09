use rusqlite::{params, Connection};

use crate::auth::audit;
use crate::error::{rule, AppResult};
use crate::model::*;
use crate::util::{new_id, optional, required};
use crate::Ctx;

// ---------------- İşletme bilgileri ----------------
pub fn get_settings(conn: &Connection) -> AppResult<BusinessSettings> {
    Ok(conn.query_row(
        "SELECT business_name, phone, address, tax_office, tax_number, contract_terms, default_option_days
         FROM settings WHERE id = 1",
        [],
        |r| {
            Ok(BusinessSettings {
                business_name: r.get(0)?,
                phone: r.get(1)?,
                address: r.get(2)?,
                tax_office: r.get(3)?,
                tax_number: r.get(4)?,
                contract_terms: r.get(5)?,
                default_option_days: r.get(6)?,
            })
        },
    )?)
}

pub fn update_settings(conn: &Connection, ctx: &Ctx, s: &BusinessSettings) -> AppResult<BusinessSettings> {
    ctx.user.require_admin()?;
    let name = required(&s.business_name, "İşletme adı", 150)?;
    if !(1..=90).contains(&s.default_option_days) {
        return rule("Ön rezervasyon süresi 1 ile 90 gün arasında olmalıdır.");
    }
    conn.execute(
        "UPDATE settings SET business_name = ?1, phone = ?2, address = ?3, tax_office = ?4, tax_number = ?5,
         contract_terms = ?6, default_option_days = ?7 WHERE id = 1",
        params![
            name,
            optional(&s.phone, 30),
            optional(&s.address, 300),
            optional(&s.tax_office, 100),
            optional(&s.tax_number, 30),
            s.contract_terms.trim(),
            s.default_option_days
        ],
    )?;
    audit(conn, ctx, "ayarlar_guncellendi", "")?;
    get_settings(conn)
}

// ---------------- Salonlar ----------------
pub fn list_halls(conn: &Connection, include_inactive: bool) -> AppResult<Vec<HallDto>> {
    let sql = if include_inactive {
        "SELECT id, name, capacity, is_active FROM halls ORDER BY is_active DESC, sort, name"
    } else {
        "SELECT id, name, capacity, is_active FROM halls WHERE is_active = 1 ORDER BY sort, name"
    };
    let mut stmt = conn.prepare(sql)?;
    let rows = stmt.query_map([], |r| {
        Ok(HallDto { id: r.get(0)?, name: r.get(1)?, capacity: r.get(2)?, is_active: r.get(3)? })
    })?;
    Ok(rows.collect::<Result<_, _>>()?)
}

pub fn save_hall(conn: &Connection, ctx: &Ctx, id: Option<&str>, request: &SaveHallRequest) -> AppResult<String> {
    ctx.user.require_admin()?;
    let name = required(&request.name, "Salon adı", 100)?;
    if !(1..=100_000).contains(&request.capacity) {
        return rule("Salon kapasitesini kişi sayısı olarak yazın (örnek: 400).");
    }
    let id = match id {
        Some(id) => {
            let changed = conn.execute(
                "UPDATE halls SET name = ?1, capacity = ?2, is_active = ?3 WHERE id = ?4",
                params![name, request.capacity, request.is_active, id],
            )?;
            if changed == 0 {
                return rule("Salon bulunamadı.");
            }
            id.to_string()
        }
        None => {
            let id = new_id();
            let sort: i64 = conn.query_row("SELECT COALESCE(MAX(sort), -1) + 1 FROM halls", [], |r| r.get(0))?;
            conn.execute(
                "INSERT INTO halls (id, name, capacity, is_active, sort) VALUES (?1, ?2, ?3, ?4, ?5)",
                params![id, name, request.capacity, request.is_active, sort],
            )?;
            id
        }
    };
    audit(conn, ctx, "salon_kaydedildi", &name)?;
    Ok(id)
}

// ---------------- Paketler ve ek hizmetler ----------------
#[derive(Clone, Copy)]
pub enum ProductTable {
    Packages,
    Extras,
}

impl ProductTable {
    fn table(self) -> &'static str {
        match self {
            ProductTable::Packages => "packages",
            ProductTable::Extras => "extras",
        }
    }

    fn label(self) -> &'static str {
        match self {
            ProductTable::Packages => "Paket",
            ProductTable::Extras => "Ek hizmet",
        }
    }
}

pub fn list_products(conn: &Connection, kind: ProductTable, include_inactive: bool) -> AppResult<Vec<ProductDto>> {
    let description = match kind {
        ProductTable::Packages => "description",
        ProductTable::Extras => "NULL",
    };
    let filter = if include_inactive { "" } else { "WHERE is_active = 1" };
    let sql = format!(
        "SELECT id, name, {description}, price, is_active FROM {} {filter} ORDER BY is_active DESC, price, name",
        kind.table()
    );
    let mut stmt = conn.prepare(&sql)?;
    let rows = stmt.query_map([], |r| {
        Ok(ProductDto { id: r.get(0)?, name: r.get(1)?, description: r.get(2)?, price: r.get(3)?, is_active: r.get(4)? })
    })?;
    Ok(rows.collect::<Result<_, _>>()?)
}

pub fn save_product(
    conn: &Connection,
    ctx: &Ctx,
    kind: ProductTable,
    id: Option<&str>,
    request: &SaveProductRequest,
) -> AppResult<String> {
    ctx.user.require_admin()?;
    let name = required(&request.name, &format!("{} adı", kind.label()), 150)?;
    if request.price < 0 {
        return rule("Fiyat eksi olamaz.");
    }
    let description = optional(&request.description, 1000);
    let table = kind.table();
    let id = match id {
        Some(id) => {
            let changed = match kind {
                ProductTable::Packages => conn.execute(
                    "UPDATE packages SET name = ?1, description = ?2, price = ?3, is_active = ?4 WHERE id = ?5",
                    params![name, description, request.price, request.is_active, id],
                )?,
                ProductTable::Extras => conn.execute(
                    "UPDATE extras SET name = ?1, price = ?2, is_active = ?3 WHERE id = ?4",
                    params![name, request.price, request.is_active, id],
                )?,
            };
            if changed == 0 {
                return rule(format!("{} bulunamadı.", kind.label()));
            }
            id.to_string()
        }
        None => {
            let id = new_id();
            match kind {
                ProductTable::Packages => conn.execute(
                    "INSERT INTO packages (id, name, description, price, is_active) VALUES (?1, ?2, ?3, ?4, ?5)",
                    params![id, name, description, request.price, request.is_active],
                )?,
                ProductTable::Extras => conn.execute(
                    "INSERT INTO extras (id, name, price, is_active) VALUES (?1, ?2, ?3, ?4)",
                    params![id, name, request.price, request.is_active],
                )?,
            };
            id
        }
    };
    audit(conn, ctx, &format!("{table}_kaydedildi"), &name)?;
    Ok(id)
}
