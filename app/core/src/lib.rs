//! Salonera çekirdeği: veritabanı, iş kuralları ve yerel ağ sunucusu.
//!
//! Ana bilgisayardaki program bu sunucuyu çalıştırır. Aynı salondaki diğer bilgisayarlar
//! ağ üzerinden bu sunucuya bağlanır; veri dosyası yalnızca ana bilgisayarda durur.

pub mod auth;
pub mod backup;
pub mod catalog;
pub mod customers;
pub mod db;
pub mod discovery;
pub mod error;
pub mod model;
pub mod payments;
pub mod reports;
pub mod reservations;
pub mod server;
pub mod util;

use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};

use chrono::{NaiveDate, NaiveDateTime, Utc};
use rusqlite::Connection;

use crate::auth::{SessionUser, Sessions};
use crate::error::{AppError, AppResult};

/// Bir isteği yapan kullanıcı ve isteğin yapıldığı an (Türkiye saati).
pub struct Ctx {
    pub user: SessionUser,
    pub now: NaiveDateTime,
}

impl Ctx {
    pub fn today(&self) -> NaiveDate {
        self.now.date()
    }
}

pub type Clock = Arc<dyn Fn() -> NaiveDateTime + Send + Sync>;

/// Türkiye 2016'dan beri yıl boyu UTC+3 kullanır.
pub fn turkey_clock() -> Clock {
    Arc::new(|| (Utc::now() + chrono::Duration::hours(3)).naive_utc())
}

pub struct App {
    db: Mutex<Connection>,
    pub sessions: Sessions,
    pub clock: Clock,
    pub backup_dir: Option<PathBuf>,
}

impl App {
    /// Veri klasöründeki `salonera.db` dosyasını açar (yoksa oluşturur).
    pub fn open(data_dir: &Path) -> AppResult<Arc<App>> {
        let conn = db::open(&data_dir.join("salonera.db"))?;
        Ok(Arc::new(App {
            db: Mutex::new(conn),
            sessions: Sessions::default(),
            clock: turkey_clock(),
            backup_dir: Some(data_dir.join("yedekler")),
        }))
    }

    /// Testler için bellekte çalışan, saati sabitlenebilen örnek.
    pub fn in_memory(clock: Clock) -> AppResult<Arc<App>> {
        Ok(Arc::new(App { db: Mutex::new(db::open_in_memory()?), sessions: Sessions::default(), clock, backup_dir: None }))
    }

    pub fn now(&self) -> NaiveDateTime {
        (self.clock)()
    }

    /// Veritabanını kilitleyip işi yapar. Bütün yazmalar sırayla yapıldığı için
    /// "çakışma kontrolü" ile "kayıt" arasına başka bir kayıt giremez.
    pub fn with_db<T>(&self, work: impl FnOnce(&mut Connection) -> AppResult<T>) -> AppResult<T> {
        let mut guard = self.db.lock().map_err(|_| AppError::Internal("veritabanı kilidi bozuldu".into()))?;
        work(&mut guard)
    }

    pub fn ctx(&self, user: SessionUser) -> Ctx {
        Ctx { user, now: self.now() }
    }
}
