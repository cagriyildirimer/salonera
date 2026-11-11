# Salonera (yeni sürüm)

Düğün salonları için rezervasyon, sözleşme ve ödeme takibi. Windows programı; ön yüz React, arka yüz Rust (Tauri).

## Nasıl çalışır?

- **Ana bilgisayar:** Bütün kayıtlar bu bilgisayarda tek bir dosyada (`salonera.db`, SQLite) durur.
  Salonera açıkken içinde küçük bir sunucu çalışır (port 47800). Her gün otomatik yedek alınır.
- **Diğer bilgisayarlar:** Aynı programı kurar, ilk açılışta "Diğer bilgisayar"ı seçer; ana bilgisayar
  ağda kendiliğinden bulunur (UDP 47811). Veri dosyası ağda paylaşılmaz.
- Veri klasörü: `%APPDATA%\com.salonera.app\veri` (yedekler `yedekler` alt klasöründe).

## Klasörler

| Klasör | İçerik |
|---|---|
| `core/` | Rust çekirdeği: veritabanı (`db.rs`), iş kuralları (`reservations.rs`, `payments.rs`…), ağ sunucusu (`server.rs`), testler (`tests/api.rs`) |
| `src-tauri/` | Windows programı kabuğu: ana bilgisayar/bağlan ayarı, sunucuyu başlatma |
| `src/` | React ekranları (`pages/`), ortak parçalar (`components/`), tasarım (`styles.css`) |

## Geliştirme

Gerekenler: Rust (stable), Node.js, Microsoft C++ Build Tools, WebView2.

```bash
npm install
npm run tauri dev          # programı geliştirme modunda açar
cargo test -p salonera-core   # iş kuralı testleri
npm test                      # arayüz testleri (ödeme planı, para biçimi)
npm run typecheck
npx tauri build            # kurulum dosyası: target/release/bundle/nsis/
```

Tarayıcıda denemek için: `cargo run -p salonera-core --example dev_server -- dev-veri` ve ayrı bir pencerede `npx vite`.

## Kurallar (özet)

- Para her yerde **kuruş** (tam sayı) olarak tutulur; yuvarlama hatası olmaz.
- Aynı salonda saatleri çakışan iki kayıt olamaz; bitişik saatler serbesttir; gece yarısını geçen kayıt ertesi günü de kapatır.
- Süresi dolan ön rezervasyon ve iptal edilen kayıt salonu boşaltır.
- Ödeme kalan borcu, iade ödenen tutarı aşamaz. Ödemeler taksitlere sırayla düşülür.
- Paket fiyatı = sabit tutar + kişi başı fiyat × davetli sayısı (davetli "en az kişi" sınırının altındaysa sınır kadar).
  Kişi başı fiyatı 0 olan paket sabit fiyatlıdır.
- Paket fiyatı sonradan değişse de yapılmış sözleşmeler değişmez (satış anındaki fiyatlar rezervasyona kopyalanır).
- Kesin kayıtta davetli sayısı değişirse program yeni fiyatı gösterip sorar; onaylanırsa fark ödenmemiş son taksite
  eklenir / ödenmemiş taksitlerden düşülür. Yeni tutar ödenmiş paradan az olamaz.
- Organizasyon planı (rezervasyon → "Organizasyon planı"): saatlik akış ve müşterinin müzikleri; "Yazdır / PDF" ile çıktı alınır.
  Akış, organizasyonun başlangıcına göre sıralanır (gece yarısını geçen düğünde 00:30 sona gelir). Müzik bağlantısı yalnızca
  http(s) adresi olabilir. İptal edilen kaydın planı değiştirilemez.
- Roller: **Yönetici** (her şey) ve **Çalışan** (rezervasyon, müşteri, ödeme alma). Ödeme kaydını iptal etmek yalnızca yöneticidedir.
- Veritabanı şeması `core/src/db.rs` içindeki `MIGRATIONS` listesindedir; değişiklik için listenin sonuna yeni adım eklenir, eski adımlar değiştirilmez.
