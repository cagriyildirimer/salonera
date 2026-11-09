# Salonera

Salonera, düğün salonları ve organizasyon işletmeleri için Windows üzerinde çalışan
çok kullanıcılı salon yönetimi ve ön muhasebe uygulamasıdır.

## Gereksinimler

- Windows 10 veya Windows 11
- .NET 10 SDK
- Visual Studio 2026 veya .NET 10 destekli güncel Visual Studio
- Çalışan bir SQL Server `SALONERA` örneği (`localhost\SALONERA`)

## Geliştirme komutları

```powershell
dotnet restore Salonera.sln
dotnet build Salonera.sln --configuration Debug --no-restore
dotnet test Salonera.sln --configuration Debug --no-build
dotnet run --project src/Salonera.Desktop/Salonera.Desktop.csproj
```

İlk açılışta veritabanı otomatik hazırlanır ve ilk yönetici hesabı oluşturulur.
Tahsilatlar ekranı, sözleşme oluşturulduktan sonra kullanılabilir.

Yerel makineye özel ve gizli ayarlar `appsettings.Local.json` dosyasına yazılmalı;
bu dosya kaynak kontrolüne alınmaz.
