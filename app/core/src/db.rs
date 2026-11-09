use std::path::Path;

use rusqlite::Connection;

use crate::error::AppResult;

/// Şema sürümleri. Yeni bir değişiklik gerektiğinde listenin sonuna yeni bir adım eklenir;
/// eski adımlar asla değiştirilmez. Böylece ana bilgisayardaki veri güncellemede korunur.
const MIGRATIONS: &[&str] = &[r#"
CREATE TABLE users (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    username TEXT NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    role TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    failed_logins INTEGER NOT NULL DEFAULT 0,
    locked_until TEXT,
    created_at TEXT NOT NULL
);
CREATE TABLE settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    business_name TEXT NOT NULL,
    phone TEXT,
    address TEXT,
    tax_office TEXT,
    tax_number TEXT,
    contract_terms TEXT NOT NULL,
    default_option_days INTEGER NOT NULL DEFAULT 7
);
CREATE TABLE halls (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    capacity INTEGER NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    sort INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE packages (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    price INTEGER NOT NULL CHECK (price >= 0),
    is_active INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE extras (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    price INTEGER NOT NULL CHECK (price >= 0),
    is_active INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE customers (
    id TEXT PRIMARY KEY,
    full_name TEXT NOT NULL,
    search_name TEXT NOT NULL,
    phone TEXT NOT NULL,
    phone2 TEXT,
    bride_name TEXT,
    groom_name TEXT,
    notes TEXT,
    is_deleted INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL
);
CREATE INDEX ix_customers_search ON customers(search_name);
CREATE TABLE reservations (
    id TEXT PRIMARY KEY,
    no TEXT NOT NULL UNIQUE,
    customer_id TEXT NOT NULL REFERENCES customers(id),
    hall_id TEXT NOT NULL REFERENCES halls(id),
    starts_at TEXT NOT NULL,
    ends_at TEXT NOT NULL,
    kind TEXT NOT NULL,
    guest_count INTEGER NOT NULL CHECK (guest_count > 0),
    status TEXT NOT NULL,
    option_until TEXT,
    package_name TEXT,
    package_price INTEGER NOT NULL DEFAULT 0,
    discount INTEGER NOT NULL DEFAULT 0,
    total INTEGER NOT NULL DEFAULT 0 CHECK (total >= 0),
    notes TEXT,
    cancel_reason TEXT,
    created_by TEXT NOT NULL REFERENCES users(id),
    created_at TEXT NOT NULL,
    confirmed_at TEXT,
    CHECK (ends_at > starts_at)
);
CREATE INDEX ix_reservations_hall_time ON reservations(hall_id, starts_at);
CREATE INDEX ix_reservations_time ON reservations(starts_at);
CREATE TABLE reservation_extras (
    id TEXT PRIMARY KEY,
    reservation_id TEXT NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    price INTEGER NOT NULL
);
CREATE TABLE installments (
    id TEXT PRIMARY KEY,
    reservation_id TEXT NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
    label TEXT NOT NULL,
    due_date TEXT NOT NULL,
    amount INTEGER NOT NULL CHECK (amount > 0),
    ord INTEGER NOT NULL
);
CREATE INDEX ix_installments_reservation ON installments(reservation_id);
CREATE TABLE payments (
    id TEXT PRIMARY KEY,
    no TEXT NOT NULL UNIQUE,
    reservation_id TEXT NOT NULL REFERENCES reservations(id),
    date TEXT NOT NULL,
    amount INTEGER NOT NULL CHECK (amount > 0),
    method TEXT NOT NULL,
    is_refund INTEGER NOT NULL DEFAULT 0,
    note TEXT,
    is_cancelled INTEGER NOT NULL DEFAULT 0,
    cancel_reason TEXT,
    created_by TEXT NOT NULL REFERENCES users(id)
);
CREATE INDEX ix_payments_reservation ON payments(reservation_id);
CREATE INDEX ix_payments_date ON payments(date);
CREATE TABLE audit (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    at TEXT NOT NULL,
    user_id TEXT,
    action TEXT NOT NULL,
    detail TEXT
);
"#];

pub fn open(path: &Path) -> AppResult<Connection> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent).map_err(|e| crate::error::AppError::Internal(e.to_string()))?;
    }
    let mut conn = Connection::open(path)?;
    conn.pragma_update(None, "journal_mode", "WAL")?;
    configure(&mut conn)?;
    Ok(conn)
}

pub fn open_in_memory() -> AppResult<Connection> {
    let mut conn = Connection::open_in_memory()?;
    configure(&mut conn)?;
    Ok(conn)
}

fn configure(conn: &mut Connection) -> AppResult<()> {
    conn.pragma_update(None, "foreign_keys", "ON")?;
    conn.pragma_update(None, "synchronous", "NORMAL")?;
    migrate(conn)
}

fn migrate(conn: &mut Connection) -> AppResult<()> {
    let current: i64 = conn.query_row("PRAGMA user_version", [], |r| r.get(0))?;
    for (index, sql) in MIGRATIONS.iter().enumerate().skip(current as usize) {
        let tx = conn.transaction()?;
        tx.execute_batch(sql)?;
        tx.pragma_update(None, "user_version", (index + 1) as i64)?;
        tx.commit()?;
    }
    Ok(())
}
