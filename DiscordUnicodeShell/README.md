# Discord Unicode Spoofer

## Discord Unicode Spoofer Demo – Automate Discord Quests

In this video we showcase the Discord Unicode Spoofer, a C# tool that automates quest timers and keeps your Discord status active. Learn how to set it up, customize titles, and generate a standalone executable.

[YouTube Video](https://youtu.be/your_video_id)

## 📖 Proje Açıklaması
Bu proje, **Discord Unicode Spoofer** adlı bir C# Windows uygulamasıdır. Uygulama, Discord içinde görev (quest) sürelerini taklit ederek ve belirli bir süre boyunca profilinizi aktif tutarak oyun deneyiminizi özelleştirmenize olanak tanır. Kullanıcı dostu bir arayüz, Windows Form tabanlı bir sayaç ve Discord Rich Presence entegrasyonu içerir.

## ✨ Özellikler
- **Başlık ve uygulama adı özelleştirilebilir**
- **Kalan süre sayacı** ve **gerçek zamanlı ilerleme çubuğu**
- **Discord Rich Presence** desteği (başlık, durum ve zaman damgası) 
- **Başlangıçta**/ **bitişte** otomatik olarak **süreyi taklit etme** (spoof) 
- **İsteğe bağlı ikon desteği** (icon.ico) 
- **Headless ve GUI modları** (sadece arka planda çalıştırma)
- **Kendi exe’nizi oluşturma** komut satırı aracı

## 🚀 Başlarken
### Gereksinimler
- .NET 9.0 SDK (Windows)
- Windows 10/11 (Uygulama WinExe)
- Discord yüklü ve çalışıyor olmalı (Rich Presence için)

### Derleme & Çalıştırma
```bash
# Projeyi klonlayın
git clone https://github.com/yourusername/DiscordUnicodeShell.git
cd DiscordUnicodeShell

# Bağımlılıkları geri yükleyin
dotnet restore

# Debug (IDE) çalıştırma
dotnet run --project DiscordUnicodeShell.csproj
```

### Tek Dosya EXE Oluşturma
```csharp
await SpooferWorker.BuildStandaloneExeAsync(
    clientId: "YOUR_DISCORD_CLIENT_ID",
    durationSeconds: 3600,               // 1 saat
    title: "My Spoof Title",
    appName: "My App",
    processName: "DiscordSpoofer",
    targetPath: "C:\Path\To\spoof.exe",
    cancellationToken: CancellationToken.None);
```
Bu yöntem, geçici bir helper exe oluşturur, onu yayınlar ve hedef yola kopyalar.

## 📦 Dosya Yapısı
```
DiscordUnicodeShell/
│   DiscordUnicodeShell.csproj
│   Program.cs
│   AppModels.cs
│   SpooferWorker.cs   # Spoof mantığı
│   WatchWorker.cs      # İzleme (watch) mantığı
│   BridgeClient.cs     # Node bridge entegrasyonu
│   DiscordIpc.cs        # Discord IPC sınıfı
│   README.md           # <-- bu dosya
│   images/             # (isteğe bağlı) UI ekran görüntüsü
└───bin/               # Derleme çıktıları
```

## 🛠️ Kullanım
```csharp
var spoofer = new SpooferWorker(
    clientId: "123456789012345678",
    durationSeconds: 1800, // 30 dakika
    title: "Quest Spoof",
    appName: "DiscordUnicode",
    processName: "Spoofer",
    customElapsedHours: 0);

// Tek exe üret
await SpooferWorker.BuildStandaloneExeAsync(
    clientId, durationSeconds, title, appName, processName,
    "C:\Spoof\discord_spoofer.exe",
    CancellationToken.None);
```

## 🙏 Katkıda Bulunma
Katkılarınızı **Pull Request** olarak gönderin. Lütfen aşağıdakilere dikkat edin:
- Kod stiline (C# 12) uymalı
- Yeni özellikler için `README` güncellenmeli
- GPL-3.0 lisansı altında

## 📜 Lisans
Bu proje **GPL‑3.0** lisansı ile dağıtılır. Detaylar için `LICENSE` dosyasına bakın.


