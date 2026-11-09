use std::path::{Path, PathBuf};

use chrono::NaiveDateTime;
use rusqlite::Connection;

use crate::error::{AppError, AppResult};
use crate::model::BackupInfo;

/// Kaç yedek saklanacağı. Eskiler otomatik silinir.
const KEEP: usize = 60;

/// Veritabanının tutarlı bir kopyasını yedek klasörüne yazar (çalışırken alınabilir).
pub fn create(conn: &Connection, dir: &Path, now: NaiveDateTime) -> AppResult<BackupInfo> {
    std::fs::create_dir_all(dir).map_err(io)?;
    let name = format!("salonera-{}.db", now.format("%Y-%m-%d-%H%M%S"));
    let path = dir.join(&name);
    conn.execute("VACUUM INTO ?1", [path.to_string_lossy().as_ref()])?;
    prune(dir)?;
    info(&path)
}

pub fn list(dir: &Path) -> AppResult<Vec<BackupInfo>> {
    let mut items: Vec<BackupInfo> = files(dir)?.iter().filter_map(|p| info(p).ok()).collect();
    items.sort_by(|a, b| b.file_name.cmp(&a.file_name));
    Ok(items)
}

/// Bugün yedek alınmış mı? Günlük otomatik yedek için.
pub fn has_backup_for(dir: &Path, now: NaiveDateTime) -> bool {
    let prefix = format!("salonera-{}", now.format("%Y-%m-%d"));
    files(dir).map(|f| f.iter().any(|p| file_name(p).starts_with(&prefix))).unwrap_or(false)
}

fn files(dir: &Path) -> AppResult<Vec<PathBuf>> {
    if !dir.exists() {
        return Ok(Vec::new());
    }
    Ok(std::fs::read_dir(dir)
        .map_err(io)?
        .filter_map(Result::ok)
        .map(|e| e.path())
        .filter(|p| file_name(p).starts_with("salonera-") && p.extension().is_some_and(|x| x == "db"))
        .collect())
}

fn prune(dir: &Path) -> AppResult<()> {
    let mut all = files(dir)?;
    all.sort();
    let excess = all.len().saturating_sub(KEEP);
    for path in all.into_iter().take(excess) {
        let _ = std::fs::remove_file(path);
    }
    Ok(())
}

fn info(path: &Path) -> AppResult<BackupInfo> {
    let meta = std::fs::metadata(path).map_err(io)?;
    let name = file_name(path);
    let stamp = name.trim_start_matches("salonera-").trim_end_matches(".db");
    let created = NaiveDateTime::parse_from_str(stamp, "%Y-%m-%d-%H%M%S")
        .map(|d| d.format("%Y-%m-%dT%H:%M:%S").to_string())
        .unwrap_or_default();
    Ok(BackupInfo { file_name: name, created_at: created, size_bytes: meta.len() })
}

fn file_name(path: &Path) -> String {
    path.file_name().map(|n| n.to_string_lossy().to_string()).unwrap_or_default()
}

fn io(error: std::io::Error) -> AppError {
    AppError::Internal(error.to_string())
}
