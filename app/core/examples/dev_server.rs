//! Geliştirme için: Salonera sunucusunu tek başına çalıştırır.
//! Kullanım: cargo run --example dev_server -- <veri-klasörü>
use std::path::PathBuf;

#[tokio::main]
async fn main() {
    let dir = PathBuf::from(std::env::args().nth(1).unwrap_or_else(|| "dev-veri".into()));
    let app = salonera_core::App::open(&dir).expect("veri açılamadı");
    println!("Salonera sunucusu: http://127.0.0.1:47800  (veri: {})", dir.display());
    salonera_core::server::serve(app, 47800).await.expect("sunucu durdu");
}
