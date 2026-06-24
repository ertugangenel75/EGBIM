# EGBIMOTO — Kurulum ve Dağıtım Kılavuzu

EGBIMOTO iki kurulum yöntemi sunar. Çalışan sistemi bozmadan ikisi de
desteklenir (hibrit mimari).

## Yöntem 1 — Basit (Tek-DLL)

En hızlı yol. Tek bir DLL seti ve `.addin` dosyası.

1. `dotnet build src/EGBIMOTO.Addin -c Release -p:RevitVersion=2026`
2. Build çıktısındaki tüm dosyaları (`EGBIMOTO.Addin.dll`, `EGBIMOTO.Core.dll`,
   bağımlılıklar, `manifests/`, `data/`, `op_contracts.json`, `categories.json`)
   şu klasöre kopyalayın:
   `%AppData%\Autodesk\Revit\Addins\2026\`
3. `deploy/addins/EGBIMOTO.addin` dosyasını aynı klasöre koyun.
4. Revit'i başlatın.

**Güncelleme:** Tüm DLL setini değiştirin (Revit kapalıyken — dosyalar kilitli olur).

## Yöntem 2 — Gelişmiş (Bootstrap + Engine Ayrımı)

Engine ayrı klasörde tutulur; güncelleme daha esnektir. MSI installer bunu otomatik yapar.

### Mimari

```
%AppData%\Autodesk\Revit\Addins\2026\
    EGBIMOTO.Bootstrap.addin       ← Revit bunu okur
    EGBIMOTO.Bootstrap.dll          ← küçük thunk (asla değişmez)

%AppData%\EGBIMOTO\R26\app\
    EGBIMOTO.Addin.dll              ← engine (güncellenebilir)
    EGBIMOTO.Core.dll
    <bağımlılıklar: WebView2, Roslyn...>
    manifests\  data\  op_contracts.json  categories.json
```

Bootstrap, engine'i `AssemblyDependencyResolver` ile yükler ve `IExternalApplication`
çağrılarını ona yönlendirir.

**Hibrit fallback:** Engine `app\` altında bulunamazsa Bootstrap, kendi yanındaki
`EGBIMOTO.Addin.dll`'i arar. Yani Yöntem 2 kurulu olsa bile tek-DLL kopyalama çalışır.

### MSI ile Kurulum

**Önkoşullar:** .NET 8 Desktop Runtime, WiX Toolset v5 (`dotnet tool install --global wix`).

```bash
# 1. Build çıktısını installer için diz
./deploy/stage.sh 2026

# 2. MSI'yı derle
dotnet build deploy/installer/EGBIMOTO.Installer.wixproj -p:RevitVersion=2026

# Çıktı: deploy/installer/bin/EGBIMOTO-R26.msi
```

Kullanıcı `EGBIMOTO-R26.msi`'a çift tıklar → per-user kurulum (yönetici hakkı gerekmez).

**Güncelleme:** Yeni MSI'ı çalıştırın. `MajorUpgrade` eski sürümü otomatik kaldırır.
Sadece `app\` içeriği değişir; `.addin` ve Bootstrap.dll sabit kalır.

## Çoklu Revit Sürümü

Her sürüm için ayrı build ve MSI:

```bash
./deploy/stage.sh 2024 && dotnet build deploy/installer/EGBIMOTO.Installer.wixproj -p:RevitVersion=2024
./deploy/stage.sh 2025 && dotnet build deploy/installer/EGBIMOTO.Installer.wixproj -p:RevitVersion=2025
./deploy/stage.sh 2026 && dotnet build deploy/installer/EGBIMOTO.Installer.wixproj -p:RevitVersion=2026
```

Engine'ler ayrı klasörlerde (`R24\`, `R25\`, `R26\`) yan yana durur, çakışmaz.

## Sorun Giderme

**Şerit görünmüyor:** Bootstrap log'una bakın:
`%AppData%\EGBIMOTO\logs\bootstrap_YYYYMMDD.log`. Engine'in hangi konumda arandığını
ve yüklenip yüklenmediğini gösterir.

**"Engine bulunamadı" hatası:** `app\EGBIMOTO.Addin.dll` eksik. MSI'ı yeniden çalıştırın
veya tek-DLL fallback için engine'i Bootstrap.dll'in yanına kopyalayın.

**Bağımlılık yükleme hatası:** `EGBIMOTO.Addin.deps.json` engine klasöründe olmalı —
`AssemblyDependencyResolver` bunu kullanır. `stage.sh` bunu otomatik kopyalar.

## Dosya Yapısı

```
deploy/
    addins/
        EGBIMOTO.addin              ← Yöntem 1 (tek-DLL)
        EGBIMOTO.Bootstrap.addin    ← Yöntem 2 (engine ayrımı)
    installer/
        EGBIMOTO.Installer.wixproj  ← WiX proje
        EGBIMOTO.Installer.wxs      ← installer tanımı
        HarvestedFiles.wxs          ← (stage.sh üretir) manifest/data component'leri
    stage.sh                        ← build → stage dizimi
    generate_components.py          ← WiX harvest üretici
```
