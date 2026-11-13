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

### Raporlar

Raporlar sayfasında (yalnızca yönetici) altı rapor var: yıllık özet, aylık rapor, kasa raporu (gün sonu dahil),
alacaklar ve tahsilat takvimi, satış analizi (paket, menü, ek hizmet, iptal) ve salon doluluğu. Her biri ekranda
grafiklerle gösterilir, **Yazdır** ile A4 çıktısı alınır (`@media print`), **Excel'e aktar** ile CSV indirilir.
Hesaplar `src/reportData.ts` içindedir; sunucu tarafında `/api/reports/sales` ve `/api/reports/due` uçları kullanılır.

### Takvim eşitleme (Google Takvim / Outlook)

Ayarlar → **Takvim eşitleme** (yalnızca yönetici). Kesin kayıtlar ve süresi dolmamış ön rezervasyonlar ("(Opsiyon)")
bağlanan hesapta Salonera'nın açtığı ayrı bir takvime ("Salonera – <işletme adı>") yazılır. İptal edilen ve süresi dolan
ön rezervasyon silinir. Etkinlikte müşteri adı, telefon, gelin–damat, salon, saat, davetli, paket, menü, ek hizmetler bulunur;
**fiyat/ödeme bilgisi gönderilmez**. Eşitleme **tek yönlüdür** (Salonera → takvim) ve ana bilgisayarda çalışır
(`core/src/calsync/`): her yazma isteğinden ~1,5 sn sonra ve 5 dakikada bir "olması gereken" ile "takvimde olan"
karşılaştırılır (`calendar_events.hash`). İnternet yoksa sonraki turda devam eder; takvimden elle silinen etkinlik
kayıt değişince geri gelir; Salonera takvimi silinmişse yeniden açılır; izin geri alınırsa ekranda "Yeniden bağla" çıkar.
Hesap bağlarken ana bilgisayarın tarayıcısı açılır (OAuth, PKCE, geri dönüş `127.0.0.1`/`localhost` geçici portu);
parola Salonera'ya gelmez, yalnızca yenileme anahtarı veritabanında saklanır.

Bunun çalışması için Salonera'nın Google ve Microsoft'ta **uygulama kaydı** olmalı; kimlikler derlemede verilir:

```bash
SALONERA_GOOGLE_CLIENT_ID=... SALONERA_GOOGLE_CLIENT_SECRET=... SALONERA_MICROSOFT_CLIENT_ID=... \
  npx tauri build --runner cargo-xwin --target x86_64-pc-windows-msvc
```

(Aynı adlı ortam değişkenleri çalışma anında da okunur; kimliği verilmeyen hizmetin düğmesi görünmez.)

- **Google:** console.cloud.google.com → yeni proje → "Google Calendar API"yi etkinleştir → OAuth izin ekranı
  (Harici; kapsamlar: `openid`, `email`, `.../auth/calendar.app.created`) → Kimlik bilgileri → OAuth istemci kimliği →
  uygulama türü **Masaüstü uygulaması**. İstemci kimliği ve "gizli anahtar" (masaüstü uygulamasında gizli sayılmaz) alınır.
  İzin ekranı "Test" durumundayken yenileme anahtarları **7 günde** geçersiz olur ve yalnızca eklenen test kullanıcıları
  bağlanabilir; gerçek kullanım için "Yayında" durumuna alın. Google doğrulaması bitene kadar giriş ekranında
  "Google bu uygulamayı doğrulamadı" uyarısı çıkar (Gelişmiş → devam) ve 100 kullanıcı sınırı vardır.
- **Microsoft:** entra.microsoft.com → Uygulama kayıtları → Yeni kayıt → hesap türü "Herhangi bir kuruluş dizini ve kişisel
  Microsoft hesapları" → Platform ekle: **Mobil ve masaüstü uygulamaları**, yönlendirme adresi `http://localhost` →
  Kimlik doğrulama → "Ortak istemci akışlarına izin ver: Evet" → API izinleri: Microsoft Graph (temsilci) `Calendars.ReadWrite`,
  `User.Read`, `offline_access`. Gizli anahtar gerekmez; "Uygulama (istemci) kimliği" yeterli.

Testler (`core/tests/calendar_sync.rs`) sahte bir Google/Graph sunucusuyla bağlanma akışını, ekleme/güncelleme/silme,
opsiyonun düşmesi, elle silinen etkinlik, anahtar yenileme ve geri alınan izni dener.

### Demo ortamı

Örnek verili, istendiğinde açılıp kapatılan tarayıcı demosu (`demo/`):

```bash
demo/salonera-demo ac       # açar, ilk seferde örnek veriyi doldurur, tarayıcıyı açar
demo/salonera-demo kapat    # kapatır; veri kalır
demo/salonera-demo sifirla  # veriyi silip bugüne göre yeniden doldurur
```

Giriş: `ayse` (yönetici), `mehmet`, `elif` (çalışan); parola `Salon2026`. Veri `~/.salonera-demo` klasöründedir.
Örnek veri (`demo/ornek_veri.py`) API üzerinden girilir; geçmiş ayların kayıt ve ödemeleri için
`demo_server` saati, veri klasöründeki `demo-saat.txt` ile geçici olarak geri alınır. Bu yalnızca demo sunucusundadır.

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
- Yemek menüleri (Ayarlar → Yemek menüleri): bölüm bölüm yemekler ve kişi başı fark (0 = paket fiyatına dahil).
  Fark × davetli (paketin "en az kişi" sınırıyla) toplama eklenir. Menü kesin kayıtta seçilir ve rezervasyona kopyalanır;
  o düğüne özel düzenlenebilir, katalog sonradan değişse de değişmez. Menü değişirse fark ödenmemiş taksitlere yansır.
  Rezervasyon → "Mutfak çıktısı": davetli sayısı, yemekler, mutfak notları ve saatlik akış.
- Ek hizmetlerde **seçenek grubu** (ör. Pasta → muzlu / çilekli): gruptan tek seçim yapılır. Ek hizmet sabit tutar ve/veya
  kişi başı fiyatla satılabilir (kişi başı kısım davetli sayısıyla, paketin "en az kişi" sınırıyla çarpılır; davetli değişince
  yeniden hesaplanır). Kesin kayıttan sonra menü sayfasından değiştirilebilir: rezervasyondakiler satış fiyatıyla kalır,
  yeni seçilen güncel fiyatla gelir, fark ödenmemiş taksitlere yansır.
- Satış ve menü ekranlarında toplam tutar altta sağda hep görünür; her seçeneğin kartında "seçilirse toplam" yazar.
- Organizasyon planı (rezervasyon → "Organizasyon planı"): saatlik akış ve müşterinin müzikleri; "Yazdır / PDF" ile çıktı alınır.
  Akış, organizasyonun başlangıcına göre sıralanır (gece yarısını geçen düğünde 00:30 sona gelir). Müzik bağlantısı yalnızca
  http(s) adresi olabilir. İptal edilen kaydın planı değiştirilemez.
- Roller: **Yönetici** (her şey) ve **Çalışan** (rezervasyon, müşteri, ödeme alma). Ödeme kaydını iptal etmek yalnızca yöneticidedir.
- Veritabanı şeması `core/src/db.rs` içindeki `MIGRATIONS` listesindedir; değişiklik için listenin sonuna yeni adım eklenir, eski adımlar değiştirilmez.
