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
"#,
// 2: Kişi sayısına göre fiyat. Paket tutarı = sabit tutar (price) + kişi başı × max(davetli, en az kişi).
// Satış anındaki fiyat bilgisi rezervasyona kopyalanır; paket sonradan değişse de sözleşme değişmez.
// reservations.package_price hesaplanmış paket tutarını tutar (eski kayıtlarda sabit fiyatla aynıdır).
r#"
ALTER TABLE packages ADD COLUMN per_guest_price INTEGER NOT NULL DEFAULT 0 CHECK (per_guest_price >= 0);
ALTER TABLE packages ADD COLUMN min_guests INTEGER NOT NULL DEFAULT 0 CHECK (min_guests >= 0);
ALTER TABLE reservations ADD COLUMN package_base_price INTEGER NOT NULL DEFAULT 0;
ALTER TABLE reservations ADD COLUMN package_per_guest INTEGER NOT NULL DEFAULT 0;
ALTER TABLE reservations ADD COLUMN package_min_guests INTEGER NOT NULL DEFAULT 0;
UPDATE reservations SET package_base_price = package_price;
"#,
// 3: Organizasyon planı: saatlik akış (yemek, pasta, takı töreni…) ve müşterinin seçtiği müzikler.
r#"
CREATE TABLE program_items (
    id TEXT PRIMARY KEY,
    reservation_id TEXT NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
    time TEXT NOT NULL,
    title TEXT NOT NULL,
    note TEXT,
    ord INTEGER NOT NULL
);
CREATE INDEX ix_program_items_reservation ON program_items(reservation_id);
CREATE TABLE music_items (
    id TEXT PRIMARY KEY,
    reservation_id TEXT NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
    label TEXT NOT NULL,
    song TEXT,
    link TEXT,
    ord INTEGER NOT NULL
);
CREATE INDEX ix_music_items_reservation ON music_items(reservation_id);
ALTER TABLE reservations ADD COLUMN program_notes TEXT;
"#,
// 4: Yemek menüleri. Menünün kişi başı farkı (0 = paket fiyatına dahil) davetli sayısıyla çarpılıp toplama eklenir.
// Seçilen menünün adı, fiyatı ve yemekleri rezervasyona kopyalanır; o düğüne özel düzenlenebilir,
// katalogdaki menü sonradan değişse de yapılmış sözleşme değişmez.
r#"
CREATE TABLE menus (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT,
    per_guest_price INTEGER NOT NULL DEFAULT 0 CHECK (per_guest_price >= 0),
    is_active INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE menu_items (
    id TEXT PRIMARY KEY,
    menu_id TEXT NOT NULL REFERENCES menus(id) ON DELETE CASCADE,
    course TEXT NOT NULL,
    name TEXT NOT NULL,
    ord INTEGER NOT NULL
);
CREATE INDEX ix_menu_items_menu ON menu_items(menu_id);
CREATE TABLE reservation_menu_items (
    id TEXT PRIMARY KEY,
    reservation_id TEXT NOT NULL REFERENCES reservations(id) ON DELETE CASCADE,
    course TEXT NOT NULL,
    name TEXT NOT NULL,
    ord INTEGER NOT NULL
);
CREATE INDEX ix_reservation_menu_items_reservation ON reservation_menu_items(reservation_id);
ALTER TABLE reservations ADD COLUMN menu_name TEXT;
ALTER TABLE reservations ADD COLUMN menu_per_guest INTEGER NOT NULL DEFAULT 0;
ALTER TABLE reservations ADD COLUMN menu_price INTEGER NOT NULL DEFAULT 0;
ALTER TABLE reservations ADD COLUMN menu_notes TEXT;
"#,
// 5: Seçenekli ek hizmetler. Aynı gruptaki ek hizmetlerden yalnızca biri seçilir (ör. "Pasta": muzlu / çilekli).
// Ek hizmet sabit tutar ve/veya kişi başı fiyatla satılabilir; kişi başı kısım davetli sayısıyla
// (paketin "en az kişi" sınırıyla) çarpılır. Satış anındaki fiyat rezervasyona kopyalanır.
// reservation_extras.price hesaplanmış tutarı tutar (eski kayıtlarda sabit fiyatla aynıdır).
r#"
ALTER TABLE extras ADD COLUMN group_name TEXT;
ALTER TABLE extras ADD COLUMN per_guest_price INTEGER NOT NULL DEFAULT 0 CHECK (per_guest_price >= 0);
ALTER TABLE reservation_extras ADD COLUMN extra_id TEXT;
ALTER TABLE reservation_extras ADD COLUMN group_name TEXT;
ALTER TABLE reservation_extras ADD COLUMN base_price INTEGER NOT NULL DEFAULT 0;
ALTER TABLE reservation_extras ADD COLUMN per_guest INTEGER NOT NULL DEFAULT 0;
UPDATE reservation_extras SET base_price = price;
"#,
// 6: Takvim eşitleme (Google Takvim / Outlook). Bağlı hesaplar ve hangi rezervasyonun takvimde hangi etkinlik olduğu.
// Eşitleme tek yönlüdür; calendar_events.hash, etkinliğin son yazılan içeriğidir (değişmediyse tekrar yazılmaz).
r#"
CREATE TABLE calendar_accounts (
    id TEXT PRIMARY KEY,
    provider TEXT NOT NULL,
    email TEXT NOT NULL,
    refresh_token TEXT NOT NULL,
    access_token TEXT,
    access_expires INTEGER,
    calendar_id TEXT NOT NULL,
    calendar_name TEXT NOT NULL,
    connected_at TEXT NOT NULL,
    connected_by TEXT,
    last_sync_at TEXT,
    last_error TEXT,
    needs_reconnect INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE calendar_events (
    account_id TEXT NOT NULL REFERENCES calendar_accounts(id) ON DELETE CASCADE,
    reservation_id TEXT NOT NULL,
    event_id TEXT NOT NULL,
    hash TEXT NOT NULL,
    PRIMARY KEY (account_id, reservation_id)
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
