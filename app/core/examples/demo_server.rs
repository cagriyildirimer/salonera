//! Demo ortamı sunucusu. dev_server ile aynıdır; tek farkı saatin geçici olarak geri alınabilmesidir:
//! veri klasöründe `demo-saat.txt` varsa (içinde "2026-05-14T10:30:00"), programın "şimdi"si o andır.
//! Örnek veri doldurucu geçmiş aylardaki kayıt ve ödemeleri bu yolla, gerçek iş kurallarından geçirerek yazar;
//! işi bitince dosyayı siler ve saat gerçek zamana döner. Gerçek programda bu özellik yoktur.
use std::path::PathBuf;
use std::sync::Arc;

use chrono::NaiveDateTime;

#[tokio::main]
async fn main() {
    let mut args = std::env::args().skip(1);
    let dir = PathBuf::from(args.next().unwrap_or_else(|| "demo-veri".into()));
    let port: u16 = args.next().and_then(|p| p.parse().ok()).unwrap_or(47800);
    std::fs::create_dir_all(&dir).expect("veri klasörü oluşturulamadı");
    let clock_file = dir.join("demo-saat.txt");
    let real = salonera_core::turkey_clock();
    let clock: salonera_core::Clock = Arc::new(move || {
        std::fs::read_to_string(&clock_file)
            .ok()
            .and_then(|s| NaiveDateTime::parse_from_str(s.trim(), "%Y-%m-%dT%H:%M:%S").ok())
            .unwrap_or_else(|| real())
    });
    let app = salonera_core::App::open_with_clock(&dir, clock).expect("veri açılamadı");
    // Takvim eşitleme düğmeleri demoda da görünsün; gerçek hesap bağlanmaz.
    app.calsync.configure(salonera_core::calsync::SyncConfig { demo: true, ..Default::default() });
    println!("Salonera demo sunucusu: http://127.0.0.1:{port}  (veri: {})", dir.display());
    salonera_core::server::serve(app, port).await.expect("sunucu durdu");
}
