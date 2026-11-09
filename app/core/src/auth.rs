use std::collections::HashMap;
use std::sync::Mutex;
use std::time::{Duration as StdDuration, Instant};

use argon2::password_hash::phc::PasswordHash;
use argon2::password_hash::{PasswordHasher, PasswordVerifier};
use argon2::Argon2;
use chrono::Duration;
use rusqlite::{params, Connection, OptionalExtension};

use crate::error::{rule, AppError, AppResult};
use crate::model::*;
use crate::util::{self, fmt_datetime, new_id, parse_datetime, required};
use crate::Ctx;

const MAX_FAILURES: i64 = 5;
const LOCK_MINUTES: i64 = 5;

#[derive(Clone, Debug)]
pub struct SessionUser {
    pub id: String,
    pub name: String,
    pub role: Role,
}

impl SessionUser {
    pub fn require_admin(&self) -> AppResult<()> {
        if self.role == Role::Admin {
            Ok(())
        } else {
            Err(AppError::Forbidden)
        }
    }
}

/// Oturumlar ana bilgisayarın belleğindedir. 14 saat işlem yapılmazsa kapanır.
#[derive(Default)]
pub struct Sessions {
    inner: Mutex<HashMap<String, (SessionUser, Instant)>>,
}

impl Sessions {
    const LIFETIME: StdDuration = StdDuration::from_secs(14 * 3600);

    pub fn create(&self, user: SessionUser) -> String {
        let token = util::new_token();
        self.inner.lock().unwrap().insert(token.clone(), (user, Instant::now()));
        token
    }

    pub fn find(&self, token: &str) -> Option<SessionUser> {
        let mut map = self.inner.lock().unwrap();
        let entry = map.get_mut(token)?;
        if entry.1.elapsed() > Self::LIFETIME {
            map.remove(token);
            return None;
        }
        entry.1 = Instant::now();
        Some(entry.0.clone())
    }

    pub fn remove(&self, token: &str) {
        self.inner.lock().unwrap().remove(token);
    }

    pub fn remove_user(&self, user_id: &str) {
        self.inner.lock().unwrap().retain(|_, (user, _)| user.id != user_id);
    }

    /// Bilgisi değişen kullanıcının açık oturumlarını günceller (ad veya rol).
    pub fn refresh_user(&self, user: &SessionUser) {
        for (existing, _) in self.inner.lock().unwrap().values_mut() {
            if existing.id == user.id {
                *existing = user.clone();
            }
        }
    }
}

pub fn hash_password(password: &str) -> AppResult<String> {
    Argon2::default()
        .hash_password(password.as_bytes())
        .map(|hash| hash.to_string())
        .map_err(|e| AppError::Internal(e.to_string()))
}

pub fn verify_password(stored: &str, password: &str) -> bool {
    match PasswordHash::new(stored) {
        Ok(parsed) => Argon2::default().verify_password(password.as_bytes(), &parsed).is_ok(),
        Err(_) => false,
    }
}

/// En az 8 karakter, en az bir harf ve bir rakam. Çalışanların hatırlayabileceği kadar sade.
pub fn ensure_strong(password: &str) -> AppResult<()> {
    let ok = password.chars().count() >= 8
        && password.chars().any(char::is_alphabetic)
        && password.chars().any(|c| c.is_ascii_digit());
    if ok {
        Ok(())
    } else {
        rule("Parola en az 8 karakter olmalı ve en az bir harf ile bir rakam içermelidir.")
    }
}

fn normalize_username(value: &str) -> AppResult<String> {
    let name = required(value, "Kullanıcı adı", 40)?.to_lowercase();
    if !name.chars().all(|c| c.is_ascii_alphanumeric() || c == '.' || c == '_') {
        return rule("Kullanıcı adında yalnızca İngilizce harf, rakam, nokta ve alt çizgi kullanılabilir (örnek: ayse.yilmaz).");
    }
    Ok(name)
}

pub fn audit(conn: &Connection, ctx: &Ctx, action: &str, detail: impl Into<String>) -> AppResult<()> {
    conn.execute(
        "INSERT INTO audit (at, user_id, action, detail) VALUES (?1, ?2, ?3, ?4)",
        params![fmt_datetime(ctx.now), ctx.user.id, action, detail.into()],
    )?;
    Ok(())
}

pub fn is_set_up(conn: &Connection) -> AppResult<bool> {
    Ok(conn.query_row("SELECT EXISTS(SELECT 1 FROM users)", [], |r| r.get(0))?)
}

pub fn server_info(conn: &Connection) -> AppResult<ServerInfo> {
    let name: Option<String> = conn
        .query_row("SELECT business_name FROM settings WHERE id = 1", [], |r| r.get(0))
        .optional()?;
    Ok(ServerInfo {
        business_name: name.unwrap_or_default(),
        is_set_up: is_set_up(conn)?,
        version: env!("CARGO_PKG_VERSION").to_string(),
    })
}

/// İlk kurulum: işletme bilgisi, ilk yönetici ve salonlar. Yalnızca bir kez yapılabilir.
pub fn setup(conn: &mut Connection, request: &SetupRequest, now: chrono::NaiveDateTime) -> AppResult<()> {
    if is_set_up(conn)? {
        return rule("Kurulum zaten yapılmış.");
    }
    let business = required(&request.business_name, "İşletme adı", 150)?;
    let admin_name = required(&request.admin_name, "Yönetici adı", 100)?;
    let username = normalize_username(&request.admin_username)?;
    ensure_strong(&request.admin_password)?;
    if request.halls.is_empty() {
        return rule("En az bir salon ekleyin.");
    }
    let tx = conn.transaction()?;
    tx.execute(
        "INSERT INTO settings (id, business_name, phone, address, contract_terms, default_option_days)
         VALUES (1, ?1, ?2, ?3, ?4, 7)",
        params![business, util::optional(&request.phone, 30), util::optional(&request.address, 300), DEFAULT_TERMS],
    )?;
    tx.execute(
        "INSERT INTO users (id, name, username, password_hash, role, created_at) VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
        params![new_id(), admin_name, username, hash_password(&request.admin_password)?, Role::Admin, fmt_datetime(now)],
    )?;
    for (index, hall) in request.halls.iter().enumerate() {
        let name = required(&hall.name, "Salon adı", 100)?;
        if hall.capacity <= 0 {
            return rule(format!("{name} için kapasiteyi yazın."));
        }
        tx.execute(
            "INSERT INTO halls (id, name, capacity, sort) VALUES (?1, ?2, ?3, ?4)",
            params![new_id(), name, hall.capacity, index as i64],
        )?;
    }
    tx.commit()?;
    Ok(())
}

pub fn login_users(conn: &Connection) -> AppResult<Vec<LoginUserOption>> {
    let mut stmt = conn.prepare("SELECT name, username FROM users WHERE is_active = 1 ORDER BY name")?;
    let rows = stmt.query_map([], |r| Ok(LoginUserOption { name: r.get(0)?, username: r.get(1)? }))?;
    Ok(rows.collect::<Result<_, _>>()?)
}

pub fn login(conn: &Connection, request: &LoginRequest, now: chrono::NaiveDateTime) -> AppResult<SessionUser> {
    let username = request.username.trim().to_lowercase();
    let row = conn
        .query_row(
            "SELECT id, name, password_hash, role, is_active, failed_logins, locked_until FROM users WHERE username = ?1",
            [&username],
            |r| {
                Ok((
                    r.get::<_, String>(0)?,
                    r.get::<_, String>(1)?,
                    r.get::<_, String>(2)?,
                    r.get::<_, Role>(3)?,
                    r.get::<_, bool>(4)?,
                    r.get::<_, i64>(5)?,
                    r.get::<_, Option<String>>(6)?,
                ))
            },
        )
        .optional()?;
    let Some((id, name, hash, role, active, failures, locked_until)) = row else {
        return rule("Kullanıcı adı veya parola hatalı.");
    };
    if !active {
        return rule("Kullanıcı adı veya parola hatalı.");
    }
    if let Some(until) = locked_until.map(|v| parse_datetime(&v)) {
        if until > now {
            let minutes = ((until - now).num_seconds() + 59) / 60;
            return rule(format!("Çok fazla hatalı deneme yapıldı. {minutes} dakika sonra tekrar deneyin."));
        }
    }
    if !verify_password(&hash, &request.password) {
        let failures = failures + 1;
        let (failures, lock) = if failures >= MAX_FAILURES {
            (0, Some(fmt_datetime(now + Duration::minutes(LOCK_MINUTES))))
        } else {
            (failures, None)
        };
        conn.execute(
            "UPDATE users SET failed_logins = ?1, locked_until = ?2 WHERE id = ?3",
            params![failures, lock, id],
        )?;
        conn.execute(
            "INSERT INTO audit (at, user_id, action) VALUES (?1, ?2, 'giris_hatali')",
            params![fmt_datetime(now), id],
        )?;
        return rule("Kullanıcı adı veya parola hatalı.");
    }
    conn.execute("UPDATE users SET failed_logins = 0, locked_until = NULL WHERE id = ?1", [&id])?;
    conn.execute(
        "INSERT INTO audit (at, user_id, action) VALUES (?1, ?2, 'giris')",
        params![fmt_datetime(now), id],
    )?;
    Ok(SessionUser { id, name, role })
}

pub fn user_dto(conn: &Connection, id: &str) -> AppResult<UserDto> {
    Ok(conn.query_row(
        "SELECT id, name, username, role, is_active FROM users WHERE id = ?1",
        [id],
        map_user,
    )?)
}

fn map_user(r: &rusqlite::Row<'_>) -> rusqlite::Result<UserDto> {
    Ok(UserDto { id: r.get(0)?, name: r.get(1)?, username: r.get(2)?, role: r.get(3)?, is_active: r.get(4)? })
}

pub fn list_users(conn: &Connection, ctx: &Ctx) -> AppResult<Vec<UserDto>> {
    ctx.user.require_admin()?;
    let mut stmt = conn.prepare("SELECT id, name, username, role, is_active FROM users ORDER BY is_active DESC, name")?;
    let rows = stmt.query_map([], map_user)?;
    Ok(rows.collect::<Result<_, _>>()?)
}

pub fn create_user(conn: &Connection, ctx: &Ctx, request: &SaveUserRequest) -> AppResult<UserDto> {
    ctx.user.require_admin()?;
    let name = required(&request.name, "Ad soyad", 100)?;
    let username = normalize_username(&request.username)?;
    let password = request.password.clone().unwrap_or_default();
    ensure_strong(&password)?;
    ensure_username_free(conn, &username, None)?;
    let id = new_id();
    conn.execute(
        "INSERT INTO users (id, name, username, password_hash, role, is_active, created_at) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)",
        params![id, name, username, hash_password(&password)?, request.role, request.is_active, fmt_datetime(ctx.now)],
    )?;
    audit(conn, ctx, "kullanici_eklendi", &username)?;
    user_dto(conn, &id)
}

pub fn update_user(conn: &Connection, ctx: &Ctx, id: &str, request: &SaveUserRequest) -> AppResult<UserDto> {
    ctx.user.require_admin()?;
    let existing = user_dto(conn, id)?;
    let name = required(&request.name, "Ad soyad", 100)?;
    let username = normalize_username(&request.username)?;
    ensure_username_free(conn, &username, Some(id))?;
    let loses_admin = existing.role == Role::Admin && (request.role != Role::Admin || !request.is_active);
    if loses_admin {
        let admins: i64 = conn.query_row(
            "SELECT COUNT(*) FROM users WHERE role = 'yonetici' AND is_active = 1 AND id <> ?1",
            [id],
            |r| r.get(0),
        )?;
        if admins == 0 {
            return rule("En az bir aktif yönetici kalmalıdır. Önce başka bir kullanıcıyı yönetici yapın.");
        }
    }
    conn.execute(
        "UPDATE users SET name = ?1, username = ?2, role = ?3, is_active = ?4 WHERE id = ?5",
        params![name, username, request.role, request.is_active, id],
    )?;
    if let Some(password) = request.password.as_deref().filter(|p| !p.is_empty()) {
        ensure_strong(password)?;
        conn.execute(
            "UPDATE users SET password_hash = ?1, failed_logins = 0, locked_until = NULL WHERE id = ?2",
            params![hash_password(password)?, id],
        )?;
    }
    audit(conn, ctx, "kullanici_guncellendi", &username)?;
    user_dto(conn, id)
}

fn ensure_username_free(conn: &Connection, username: &str, except: Option<&str>) -> AppResult<()> {
    let taken: bool = conn.query_row(
        "SELECT EXISTS(SELECT 1 FROM users WHERE username = ?1 AND id <> ?2)",
        params![username, except.unwrap_or("")],
        |r| r.get(0),
    )?;
    if taken {
        return rule("Bu kullanıcı adı başka biri tarafından kullanılıyor.");
    }
    Ok(())
}

pub fn change_password(conn: &Connection, ctx: &Ctx, request: &ChangePasswordRequest) -> AppResult<()> {
    let hash: String = conn.query_row("SELECT password_hash FROM users WHERE id = ?1", [&ctx.user.id], |r| r.get(0))?;
    if !verify_password(&hash, &request.current_password) {
        return rule("Mevcut parolanız hatalı.");
    }
    ensure_strong(&request.new_password)?;
    conn.execute(
        "UPDATE users SET password_hash = ?1 WHERE id = ?2",
        params![hash_password(&request.new_password)?, ctx.user.id],
    )?;
    audit(conn, ctx, "parola_degisti", "")
}

pub const DEFAULT_TERMS: &str = "1. Kapora, organizasyon tarihinin müşteri adına ayrılması karşılığında alınır.
2. Kalan tutar, ödeme planında belirtilen tarihlerde ödenir. Son ödeme organizasyondan önce tamamlanır.
3. Müşteri organizasyonu iptal ederse alınan kapora iade edilmez.
4. Tarih değişikliği, salonun müsait olması şartıyla bir kez ücretsiz yapılabilir.
5. Davetli sayısı sözleşmede belirtilen sayıyı aşarsa ek ücret uygulanabilir.
6. Salona dışarıdan yiyecek ve içecek getirilemez.
7. Salon eşyalarına verilen zarar müşteri tarafından karşılanır.";
