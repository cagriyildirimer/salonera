# Salonera Proje Hafızası

Son güncelleme: 9 Kasım 2026

Bu belge, yeni bir geliştirme oturumunda projenin mevcut durumunu hızlıca anlamak ve
kaldığı yerden güvenli şekilde devam etmek için hazırlanmıştır.

## 1. Ürün kararları

- Ürün adı: **Salonera**
- Amaç: Düğün salonları ve organizasyon işletmeleri için salon yönetimi ve ön muhasebe
- Arayüz dili: Türkçe
- Hedef istemci: Windows 10 ve Windows 11 üzerinde WPF masaüstü uygulaması
- Geliştirme ortamı: Linux; WPF görsel testleri Windows VM üzerinde yapılacak
- Kullanım şekli: Aynı şubedeki birden fazla bilgisayar yerel ağ üzerinden ortak veritabanı kullanacak
- Veritabanı: SQL Server Express
- Şube yaklaşımı: Her şube kendi yerel veritabanıyla çalışabilir; şubeler arası veri aktarımı sonraki sürüme bırakıldı
- Kimlikler: Gelecekteki şube senkronizasyonuna uygun GUID
- Roller: Yönetici, Satış, Muhasebe, Görüntüleme
- Para birimleri: TRY, USD, EUR
- Ana raporlama para birimi: TRY
- Döviz kuru: İşlem anında kullanıcı tarafından girilir; özgün tutar, kur ve TL karşılığı sabitlenir
- KDV: Kullanıcı oranı elle girer
- Yedekleme: İlk sürümde yerel
- SMS, WhatsApp, e-posta, bulut yedekleme ve e-belge entegrasyonları ilk sürüm dışında
- Uygulama resmî muhasebe yazılımı değil, ön muhasebe uygulamasıdır

## 2. Teknik mimari

- .NET 10 LTS
- C# ve WPF
- MVVM
- Entity Framework Core 10.0.14
- SQL Server / SQL Server Express
- Microsoft.Extensions.Hosting ve Dependency Injection
- Katmanlı mimari:
  - `Salonera.Domain`
  - `Salonera.Application`
  - `Salonera.Infrastructure`
  - `Salonera.Desktop`
  - `Salonera.UnitTests`
  - `Salonera.IntegrationTests`
- Para alanları `decimal`
- Kalıcı zamanlar UTC; kullanıcı arayüzü Türkiye saat dilimi
- Bütün temel tablolarda GUID, audit alanları, soft-delete ve `rowversion`
- Uyarılar derleme hatası kabul edilir
- Merkezi paket sürüm yönetimi `Directory.Packages.props` dosyasındadır

## 3. Tamamlanan aşamalar

### AŞAMA 0 - Analiz

- Kullanım şekli ve önemli ürün kararları netleştirildi.
- MVP, kapsam dışı özellikler ve kabul kriterleri belirlendi.
- SQL Server Express ve şube başına yerel veritabanı kararı alındı.

### AŞAMA 1 - Proje altyapısı

- Solution ve altı proje oluşturuldu.
- DI, yapılandırma, dosya loglama ve Türkiye saat servisi kuruldu.
- WPF başlangıç kabuğu ve mimari bağımlılık testleri hazırlandı.
- Günlükler `%LOCALAPPDATA%/Salonera/logs` altında tutulur.

### AŞAMA 2 - Veritabanı

- 32 temel EF Core varlığı tanımlandı.
- İlişkiler, foreign key'ler, filtreli unique index'ler ve check constraint'ler eklendi.
- Generic repository ve Unit of Work altyapısı kuruldu.
- Otomatik migration başlangıç servisi hazırlandı.
- `InitialCreate` migration'ı ve idempotent SQL betiği üretildi.
- Başlangıç verileri:
  - Merkez Şube
  - Yönetici, Satış, Muhasebe ve Görüntüleme rolleri
  - Diğer Gelir ve Diğer Gider kategorileri

### AŞAMA 3 - Kullanıcı ve yetki

- İlk yönetici oluşturma ekranı eklendi.
- Sabit varsayılan parola kullanılmaz; yönetici ilk kurulumda kendi güçlü parolasını belirler.
- Parolalar ASP.NET Core Identity PBKDF2 hasher ile saklanır.
- En az 12 karakter, büyük/küçük harf, rakam ve özel karakter politikası uygulanır.
- Giriş, zorunlu parola değiştirme ve kullanıcı yönetimi ekranları hazırlandı.
- Rol-izin matrisi servis katmanında uygulanır.
- Başarılı/başarısız girişler ve kritik kullanıcı işlemleri audit log'a yazılır.

### AŞAMA 4 - Müşteri, salon ve takvim

- Müşteri CRUD ve soft-delete servisleri eklendi.
- Şube, salon ve organizasyon türü yönetimi hazırlandı.
- Günlük, haftalık ve aylık liste tabanlı takvim eklendi.
- Opsiyon kaydı, opsiyon bitiş doğrulaması ve iptal akışı hazırlandı.
- Salon çakışma kuralı:
  - `mevcut.StartsAt < yeni.EndsAt && mevcut.EndsAt > yeni.StartsAt`
  - İptal edilen kayıtlar engel oluşturmaz.
  - Bitişik zaman aralıklarına izin verilir.
- SQL Server'da eşzamanlı kayıt güvenliği için `SERIALIZABLE` transaction ve salon bazlı `sp_getapplock` kullanılır.

### AŞAMA 5 - Satış ve sözleşme

- Paket ve ek hizmet yönetimi eklendi.
- Ortak decimal fiyatlama servisi oluşturuldu.
- İndirim, manuel KDV ve döviz kuru hesaplamaları hazırlandı.
- Paket ve ek hizmet fiyatları sözleşme anında `EventService` üzerine snapshot olarak kopyalanır.
- Paket fiyatı sonradan değiştiğinde eski sözleşme etkilenmez.
- Sözleşme oluşturulunca organizasyon `Contracted` durumuna geçer ve durum geçmişi yazılır.
- A4 Türkçe sözleşme PDF'i oluşturulur.
- PDF açma ve Windows varsayılan PDF uygulaması üzerinden yazdırma akışı eklendi.
- Takvim satırına çift tıklayarak organizasyon detayı açılır.

## 4. Mevcut test durumu

- Unit test: 2
- Integration test: 18
- Toplam: **20/20 başarılı**
- Derleme: **0 hata, 0 uyarı**
- Bekleyen EF model değişikliği: Yok
- NuGet güvenlik taraması: Bilinen açık yok

Başlıca doğrulanan senaryolar:

- Katman bağımlılıkları
- DI ve dosya loglama
- Migration SQL üretimi
- Soft-delete ve rowversion
- Parola hashleme ve rol izinleri
- Başarısız giriş audit kaydı
- Salon tarih/saat çakışmaları
- İptal edilen organizasyonun zamanı serbest bırakması
- Müşteri CRUD
- Paket fiyatı snapshot'ı
- İndirim, KDV ve döviz hesabı
- Sözleşme durum geçmişi
- Geçerli A4 PDF üretimi ve Türkçe karakterler

Standart doğrulama komutları:

```bash
dotnet restore Salonera.sln
dotnet format Salonera.sln --verify-no-changes --no-restore
dotnet build Salonera.sln --configuration Debug --no-restore
dotnet test Salonera.sln --configuration Debug --no-build
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/Salonera.Infrastructure/Salonera.Infrastructure.csproj \
  --startup-project src/Salonera.Infrastructure/Salonera.Infrastructure.csproj \
  --context SaloneraDbContext
dotnet list Salonera.sln package --vulnerable --include-transitive
```

Linux geliştirme ortamında .NET 10 SDK geçici olarak `/tmp/salonera-dotnet` altında
kullanıldı. Kalıcı geliştirme ortamında .NET 10 SDK PATH üzerinde bulunmalıdır.

## 5. Windows VM manuel test akışı

1. SQL Server Express ve .NET 10 SDK kur.
2. `SALONERA_CONNECTION_STRING` veya `appsettings.Local.json` ile bağlantıyı ayarla.
3. Migration'ı uygula veya uygulamayı başlatıp otomatik migration'ı çalıştır.
4. İlk yönetici hesabını oluştur.
5. Şube, salon ve organizasyon türü ekle.
6. Müşteri ve takvim kaydı oluştur.
7. Çakışan salon kaydının engellendiğini doğrula.
8. Paket ve ek hizmet oluştur.
9. Sözleşme ve PDF üret.
10. PDF açma ve yazdırmayı doğrula.

Örnek sözleşme: `output/pdf/Salonera_Ornek_Sozlesme.pdf`

## 6. Bilinen eksikler

- WPF ekranları Linux'ta çalıştırılamaz; Windows VM görsel testi gereklidir.
- Oturum kapatma butonu henüz yok.
- Kullanıcı arayüzünden çoklu rol atama ve yönetici parola sıfırlama yok.
- Takvim görsel zaman çizelgesi yerine liste tabanlıdır; sürükle-bırak yok.
- Bazı ayrıntılı müşteri ve salon düzenleme servisleri hazır olsa da hızlı ekranlarda tüm alanlar gösterilmiyor.
- Paket alt kalemleri, menü ve personel gibi ayrıntıların düzenleme ekranı yok.
- Sözleşme logosu, işletme/vergi bilgileri ve kullanıcı tarafından düzenlenebilir özel maddeler AŞAMA 9'a bağlıdır.
- Gerçek SQL Server üzerinde iki istemcili eşzamanlı çakışma testi Windows VM'de yapılmalıdır.

## 7. Kalan geliştirme planı

### AŞAMA 6 - Tahsilatlar

- Kapora ve taksit planı
- Kısmi tahsilat
- Kalan borç hesabı
- Fazla ödeme kontrolü
- Tahsilat iptali ve iade
- Gecikmiş tahsilat uyarıları
- Tahsilat makbuzu ve PDF/yazdırma
- Kasa/banka hareketinin aynı transaction içinde oluşturulması

### AŞAMA 7 - Gelir, gider ve cari hesaplar

- Gelir ve gider yönetimi
- Tedarikçiler ve cari hareketler
- Organizasyon maliyetleri
- Kasa ve banka hesapları
- Hesaplar arası virman
- Finansal ters kayıt ve idempotency

### AŞAMA 8 - Ön muhasebe ve raporlar

- Günlük kasa
- Gelir-gider ve kâr/zarar
- Organizasyon kârlılığı
- Müşteri ve tedarikçi bakiyeleri
- Rapor filtreleri
- Excel, CSV ve PDF dışa aktarım

### AŞAMA 9 - Yedekleme ve ayarlar

- Güvenli SQL Server yedekleme ve geri yükleme
- Otomatik günlük yedek
- Bütünlük doğrulaması ve saklama politikası
- İşletme adı, logo, vergi bilgileri ve belge numarası ayarları
- Kategori ve hesap ayarları

### AŞAMA 10 - Kullanılabilirlik

- Ana navigasyonun sol menü tasarımına dönüştürülmesi
- Form doğrulamaları ve Türkçe hata mesajları
- Klavye kısayolları
- Yüksek kontrast ve farklı çözünürlük testleri
- Büyük veri ve performans testleri

### AŞAMA 11 - Kurulum paketi

- Release build
- Windows installer
- Masaüstü ve Başlat menüsü kısayolları
- İlk kurulum sihirbazı
- SQL Server Express bağlantı/kurulum yönlendirmesi
- Temiz Windows ortamında kurulum testi

## 8. Devam noktası

Bir sonraki ana çalışma **AŞAMA 6 - Tahsilatlar** olmalıdır. Her ana aşama sonunda
derleme ve testler başarılı olmadan sonraki aşamaya geçilmemelidir.
