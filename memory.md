# Salonera Proje Hafızası

Son güncelleme: 9 Kasım 2026

Bu belge, yeni bir geliştirme oturumunda projenin mevcut durumunu hızlıca anlamak ve
kaldığı yerden güvenli şekilde devam etmek için hazırlanmıştır.

## 1. Ürün kararları

- Ürün adı: **Salonera**
- Amaç: Düğün salonları ve organizasyon işletmeleri için salon yönetimi ve ön muhasebe
- Arayüz dili: Türkçe
- Hedef istemci: Windows 10 ve Windows 11 üzerinde WPF masaüstü uygulaması
- Geliştirme ve ilk çalıştırma ortamı: Windows; WPF ekranları kullanıcıyla birlikte görsel olarak doğrulanacak
- Kullanım şekli: Aynı şubedeki birden fazla bilgisayar yerel ağ üzerinden ortak veritabanı kullanacak
- Veritabanı: SQL Server Express
- Yerel SQL Server örnek adı: `SALONERA` (`localhost\SALONERA`)
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

### AŞAMA 6 - Tahsilatlar

- Sözleşmeye bağlı kapora ve taksit planı, kısmi tahsilat ve kalan borç hesabı eklendi.
- Sözleşme ve taksit bakiyesini aşan ödemeler engellenir.
- Tahsilat iptali, kısmi iade ve finansal ters hareket kaydı eklendi.
- Gecikmiş taksitler tahsilat ekranında uyarı olarak gösterilir.
- Tahsilat makbuzu PDF olarak oluşturulur, açılır ve yazdırılır.
- Nakit ve banka hesap hareketleri tahsilatla aynı veritabanı işleminde kaydedilir.
- Gerçek `SALONERA` SQL Server örneğinde taksit, tahsilat, iade ve iptal akışı geçici test veritabanıyla doğrulandı.

### AŞAMA 7 - Gelir, gider ve cari hesaplar

- Kasa ve banka hesabı açma, bakiyeyi ve hareketleri izleme, aynı para birimindeki hesaplar arasında virman eklendi.
- Gelir ve gider kategorileri, gelir kaydı, gider kaydı ve kısmi gider ödemesi eklendi.
- Tedarikçi kaydı, gider borcu ve ödeme hareketleriyle güncellenen cari hesap eklendi.
- Organizasyon maliyeti paket/hizmet maliyet anlık görüntüsü ve doğrudan giderlerden TRY olarak hesaplanır.
- Gelir, gider ve virman iptallerinde orijinal hareket saklanır, ters finansal hareket oluşturulur.
- İşlem anahtarları tekrar denemelerde aynı finansal kaydın ikinci kez yazılmasını engeller.
- Tahsilat ekranında kasa/banka hesabı seçilebilir; boş bırakılırsa varsayılan hesap kullanılır.
- Ana gezinme sol menüye taşındı; Finans ve Cari Hesaplar ekranı eklendi.
- Gerçek `SALONERA` SQL Server örneğinde geçici test veritabanıyla gelir, gider, cari, virman ve iptal akışı doğrulandı.

### AŞAMA 8 - Ön muhasebe ve raporlar

- Günlük kasa, aylık gelir-gider/kâr-zarar, organizasyon kârlılığı, müşteri ve tedarikçi bakiyeleri eklendi.
- Tarih aralığı, salon, müşteri ve tedarikçi filtreleri ilgili raporlarda kullanılabilir.
- Raporlar UTF-8 CSV, Excel XLSX ve PDF olarak dışa aktarılır.
- İptal edilmiş tahsilatların ve iadelerin müşteri bakiyesine etkisi, iptal edilmiş finans kayıtları ve ters kasa hareketleri hesaplamalarda dikkate alınır.
- Gerçek `SALONERA` SQL Server örneğinde geçici veritabanıyla rapor tutarları ve çıktı dosyaları doğrulandı.

## 4. Mevcut test durumu

- Unit test: 2
- Integration test: 23
- Toplam: **25/25 başarılı**
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
- Taksit, kısmi ödeme, fazla ödeme, iade, iptal ve finansal hareketler
- Gerçek SQL Server üzerinde tahsilat işlemi
- Kasa/banka bakiyesi, gelir-gider, tedarikçi carisi, virman ve ters hareketler
- Gerçek SQL Server üzerinde finans akışı

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

Windows geliştirme ortamında .NET 10 SDK 10.0.111 ve `SALONERA` adlı SQL Server örneği kullanılır.

## 5. Windows VM manuel test akışı

1. SQL Server `SALONERA` örneğini ve .NET 10 SDK'yı kur.
2. `SALONERA_CONNECTION_STRING` veya `appsettings.Local.json` ile bağlantıyı ayarla.
3. Migration'ı uygula veya uygulamayı başlatıp otomatik migration'ı çalıştır.
4. İlk yönetici hesabını oluştur.
5. Şube, salon ve organizasyon türü ekle.
6. Müşteri ve takvim kaydı oluştur.
7. Çakışan salon kaydının engellendiğini doğrula.
8. Paket ve ek hizmet oluştur.
9. Sözleşme ve PDF üret.
10. PDF açma ve yazdırmayı doğrula.
11. Tahsilat planı oluştur; kısmi ödeme, iade, iptal ve makbuz PDF'ini dene.
12. Finans ekranında kasa/banka hesabı, gelir, gider, tedarikçi ve virman oluştur.
13. Gider ödemesi ve iptallerden sonra hesap ve cari bakiyelerini kontrol et.

Örnek sözleşme: `output/pdf/Salonera_Ornek_Sozlesme.pdf`

## 6. Bilinen eksikler

- Yeni rapor ekranı kullanıcıyla görsel ve elle denenmelidir.
- Virman ilk sürümde aynı para birimindeki hesaplarla sınırlıdır.
- Windows çalışma kopyasında eski dosyaların CRLF satır sonları `.editorconfig` LF kuralıyla çakıştığından, tüm çözümde `dotnet format --verify-no-changes` satır sonu hataları verir. Yeni C# dosyaları hedefli biçimlendirmeden geçirildi.
- Tek pencereli yeni arayüzde giriş, yönetim ekranları ve organizasyon ayrıntısı içerik alanında açılır; geri ve oturumu kapat düğmeleri vardır.
- Kullanıcı arayüzünden çoklu rol atama ve yönetici parola sıfırlama yok.
- Takvim görsel zaman çizelgesi yerine liste tabanlıdır; sürükle-bırak yok.
- Bazı ayrıntılı müşteri ve salon düzenleme servisleri hazır olsa da hızlı ekranlarda tüm alanlar gösterilmiyor.
- Paket alt kalemleri, menü ve personel gibi ayrıntıların düzenleme ekranı yok.
- Sözleşme logosu, işletme/vergi bilgileri ve kullanıcı tarafından düzenlenebilir özel maddeler AŞAMA 9'a bağlıdır.
- Gerçek SQL Server üzerinde iki istemcili eşzamanlı çakışma testi Windows VM'de yapılmalıdır.

## 7. Kalan geliştirme planı

### AŞAMA 9 - Yedekleme ve ayarlar

- Güvenli SQL Server yedekleme ve geri yükleme
- Otomatik günlük yedek
- Bütünlük doğrulaması ve saklama politikası
- İşletme adı, logo, vergi bilgileri ve belge numarası ayarları
- Kategori ve hesap ayarları

### AŞAMA 10 - Kullanılabilirlik

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

Bir sonraki ana çalışma **AŞAMA 9 - Yedekleme ve ayarlar** olmalıdır. Tahsilat, finans ve rapor
ekranları kullanıcıyla elle denenmelidir. Her ana aşama sonunda derleme ve testler
başarılı olmadan sonraki aşamaya geçilmemelidir.
