# Sistem Mimarisi

## 1. Büyük resim

Projeyi bir **kargo merkezi** gibi düşünebilirsiniz:

- React arayüzü müşterinin işlem yaptığı gişedir.
- ASP.NET Core API, talebi kontrol edip doğru birime yönlendiren merkezdir.
- `FtpService`, merkez ile FTP deposu arasında çalışan kurye aracıdır.
- `FtpServerInstance`, uygulamanın kendi bünyesinde açtığı FTP şubesidir.
- OpenSSH, aynı depoya açılan güvenli ve şifreli ikinci kapıdır.
- ngrok, bu güvenli kapıya internetten ulaşan geçici bir adres tabelasıdır.
- LiteDB, kullanıcılar, roller, oturumlar, sunucular ve loglar için küçük yerel kayıt dolabıdır.

```mermaid
flowchart TB
    subgraph Client["İstemci katmanı"]
        Browser["Tarayıcı / React"]
        FileZilla["FileZilla veya SFTP istemcisi"]
    end

    subgraph Application["Uygulama katmanı"]
        Api["ASP.NET Core API"]
        Access["AccessService"]
        FtpClient["FtpService + FluentFTP"]
        Manager["LocalFtpServer"]
        Logs["LogService"]
    end

    subgraph Transport["Aktarım katmanı"]
        BuiltInFtp["FtpServerInstance"]
        OpenSsh["Windows OpenSSH"]
        Ngrok["ngrok TCP tüneli"]
    end

    subgraph Storage["Veri katmanı"]
        LiteDb["LiteDB"]
        Data["C:/ProgramData/FtpManager/ftp_root/{serverId}/data"]
        LogFiles["JSONL + metin logları"]
    end

    Browser --> Api
    Api --> Access
    Api --> FtpClient
    Api --> Manager
    Api --> Logs
    Access --> LiteDb
    Manager --> LiteDb
    Logs --> LiteDb
    Logs --> LogFiles
    FtpClient --> BuiltInFtp
    BuiltInFtp --> Data
    FileZilla --> Ngrok
    Ngrok --> OpenSsh
    OpenSsh --> Data
```

## 2. Katmanlar ve sorumluluk sınırları

```mermaid
flowchart LR
    A["Sunum\nReact bileşenleri"] --> B["HTTP sınırı\nController'lar"]
    B --> C["İş kuralları\nService'ler"]
    C --> D["Protokoller\nFTP, SFTP, TCP"]
    C --> E["Kalıcılık\nLiteDB ve dosya sistemi"]
    D --> F["Windows servisleri\nOpenSSH ve yerel hesaplar"]
```

| Katman | Ana dosyalar | Sorumluluk |
| --- | --- | --- |
| Sunum | `App.jsx`, `components/*.jsx` | Kullanıcı etkileşimi, görünüm ve istemci durumu |
| HTTP | `FtpController`, `AccessController` | İstekleri almak, yanıt ve hata kodu üretmek |
| İş kuralları | `AccessService`, `LocalFtpServer`, `FtpService` | Yetki, sunucu yaşam döngüsü ve dosya işlemleri |
| Protokol | `FtpServerInstance`, `OpenSshSftpProvisioner`, `NgrokTunnelService` | FTP komutları, SFTP yapılandırması ve TCP tüneli |
| Kalıcılık | `ServerStorage`, LiteDB kullanan servisler | Dosya dizilimi, kullanıcı/sunucu/log kayıtları |

## 3. FTP, SFTP ve ngrok birbirinden nasıl ayrılır?

```mermaid
flowchart TB
    UI["Web arayüzü"] -->|"HTTP isteği"| API["API"]
    API -->|"FluentFTP / 127.0.0.1:212x"| FTP["Yerel FTP sunucusu"]
    FTP --> DATA["Ortak data klasörü"]

    FZ["FileZilla"] -->|"Şifreli SFTP"| PUBLIC["ngrok host:port"]
    PUBLIC -->|"TCP yönlendirme"| SSH["127.0.0.1:2222 OpenSSH"]
    SSH --> DATA
```

Önemli sonuçlar:

- Web arayüzü mevcut sürümde doğrudan SFTP istemcisi değildir; dosya işlemlerini FTP üzerinden yapar.
- FileZilla SFTP ile bağlandığında aynı fiziksel `data` klasörünü görür.
- Web arayüzündeki FTP kökü `/`, SFTP tarafında `/data` olarak görünür.
- ngrok dosya saklamaz, kullanıcı hesabı oluşturmaz ve FTP'yi SFTP'ye dönüştürmez. Yalnızca bir TCP bağlantısını taşır.

## 4. Depolama modeli

```mermaid
flowchart TB
    ROOT["C:/ProgramData/FtpManager/ftp_root"]
    ROOT --> S1["default"]
    ROOT --> S2["ca8f... serverId"]
    S1 --> D1["data"]
    S2 --> D2["data"]
    D1 --> F1["Varsayılan FTP dosyaları"]
    D2 --> F2["Özel sunucu dosyaları"]
```

Her sunucu kendi kimliği altında ayrılır. `ServerStorage.EnsureLayout` şu sözleşmeyi korur:

```text
ftp_root/
└── {serverId}/              # SFTP chroot; kullanıcı burada yazamaz
    └── data/                # FTP kökü ve SFTP yazılabilir alanı
```

Bu ayrım güvenlik içindir. Chroot kökü yazılabilir olursa kullanıcı kendi hapishane duvarını değiştirebilir; bu nedenle yalnızca `data` yazılabilir.

## 5. Kimlik ve yetki modeli

```mermaid
flowchart LR
    AppUser["AppUser"] -->|"RoleId"| Role["AppRole"]
    Role --> Permissions["PermissionKeys listesi"]
    AppUser --> Session["UserSession / 12 saat"]
    Session --> Token["Bearer token"]
    Token --> Request["API isteği"]
```

| Kimlik türü | Saklandığı yer | Kullanıldığı yer |
| --- | --- | --- |
| Uygulama kullanıcısı | LiteDB `users` | Panel girişi ve rol tabanlı özellikler |
| Uygulama oturumu | LiteDB `sessions` | `Authorization: Bearer ...` |
| FTP hesabı | LiteDB `servers` | FluentFTP ve yerel FTP `USER/PASS` |
| SFTP hesabı | LiteDB + Windows yerel hesap veritabanı | OpenSSH parola doğrulaması |

## 6. Süreç ve servis yaşam döngüsü

```mermaid
stateDiagram-v2
    [*] --> ApiStarting
    ApiStarting --> LoadServerConfigs
    LoadServerConfigs --> RepairSftp: SFTP etkinse
    LoadServerConfigs --> StartFtp: SFTP etkin değilse
    RepairSftp --> StartFtp: Başarılı veya hata loglandı
    StartFtp --> Running
    Running --> Stopped: Kullanıcı durdurur
    Stopped --> Running: Kullanıcı başlatır
    Running --> Deleted: Özel sunucu silinir
    Deleted --> [*]
```

`LocalFtpServer` hem singleton servis hem de `BackgroundService` olarak çalışır. API başlarken aktif sunucuları LiteDB'den okur, gerekiyorsa SFTP ayarlarını tazeler ve her biri için bir `FtpServerInstance` başlatır.

## 7. Ağ kapıları

```mermaid
flowchart LR
    Browser["Tarayıcı"] -->|"5173"| Vite["Vite geliştirme sunucusu"]
    Vite -->|"HTTP 5230"| Api["ASP.NET Core"]
    Api -->|"FTP 2121, 2122..."| Ftp["Yerel FTP instance'ları"]
    Ftp -->|"PASV 50000-51000"| DataChannel["FTP veri kanalı"]
    Ngrok["ngrok"] -->|"TCP"| Ssh["OpenSSH 2222"]
```

FTP iki kanal kullanır: komut kanalı (`2121`, `2122` gibi) ve listeleme/yükleme için veri kanalı (`50000–51000`). SFTP ise tek SSH bağlantısı üzerinden çalışır.

## 8. Bağımlılık yönü

```mermaid
flowchart TD
    FC["FtpController"] --> FS["FtpService"]
    FC --> LFS["LocalFtpServer"]
    FC --> AS["AccessService"]
    FC --> NG["NgrokTunnelService"]
    AC["AccessController"] --> AS
    LFS --> INST["FtpServerInstance"]
    LFS --> PROV["OpenSshSftpProvisioner"]
    PROV --> WU["WindowsLocalUserManager"]
    PROV --> SS["ServerStorage"]
    FS --> FL["FluentFTP"]
    PROV --> SSHNET["SSH.NET doğrulama istemcisi"]
```

Bu diyagram, bir hata gördüğünüzde nereden başlamanız gerektiğini de söyler. Örneğin FileZilla parolayı kabul edip klasörü açamıyorsa `NgrokTunnelService` değil, `OpenSshSftpProvisioner` ve NTFS ACL katmanı incelenmelidir.

