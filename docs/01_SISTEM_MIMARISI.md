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
        OpenSsh["Linux veya Windows OpenSSH"]
        Ngrok["ngrok TCP tüneli"]
    end

    subgraph Storage["Veri katmanı"]
        LiteDb["LiteDB"]
        Data["Docker volume veya C:/ProgramData/.../{serverId}/data"]
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
    D --> F["İşletim sistemi\nLinux/Windows OpenSSH ve hesaplar"]
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
    API -->|"FluentFTP / container içi veya 127.0.0.1"| FTP["Yerel FTP sunucusu"]
    FTP --> DATA["Ortak data klasörü"]

    FZ["FileZilla"] -->|"Şifreli SFTP"| PUBLIC["ngrok host:port"]
    PUBLIC -->|"TCP yönlendirme"| SSH["Yapılandırılmış SFTP portu / OpenSSH"]
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
    MODE{"Çalışma modu"}
    MODE -->|"Docker"| VOL["ftp_manager_uploads volume'u\n/app/uploads/ftp_root"]
    MODE -->|"Yerel Windows"| WIN["C:/ProgramData/FtpManager/ftp_root"]
    VOL --> S1["default/data"]
    VOL --> S2["serverId/data"]
    WIN --> S1
    WIN --> S2
```

LiteDB ve loglar Docker'da `ftp_manager_logs`, FTP/SFTP dosyaları `ftp_manager_uploads`, OpenSSH anahtarları ve yapılandırması `ftp_manager_ssh` volume'ünde tutulur. Container yeniden oluşturulsa da bu volume'ler korunur. Yerel Windows modunda veritabanı ve loglar proje altındaki `Backend/FtpManager.Api/logs`, güvenli FTP kökü ise `C:/ProgramData/FtpManager/ftp_root` konumundadır.

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
| SFTP hesabı | LiteDB + Docker Linux hesabı veya Windows yerel hesabı | OpenSSH parola doğrulaması |

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
    Browser["Tarayıcı"] -->|"Dinamik UI_PORT"| Nginx["Docker Nginx"]
    Nginx -->|"/api → backend:8080"| Api["ASP.NET Core"]
    Dev["Yerel geliştirme"] -->|"5173 → 5230"| Api
    Api -->|"Dinamik FTP_PORT_MIN-MAX"| Ftp["Yerel FTP instance'ları"]
    Ftp -->|"Dinamik PASV aralığı"| DataChannel["FTP veri kanalı"]
    Ngrok["ngrok"] -->|"TCP"| Ssh["Dinamik SFTP_PORT"]
```

Docker portları ilk çalıştırmada boş bloklardan seçilip `.docker/runtime.env` dosyasına yazılır; sonraki başlatmalarda aynı değerler kullanılır. Yerel geliştirme varsayılanları Vite için `5173`, API için `5230`, ilk FTP instance'ı için `2121`, SFTP için `2222` ve pasif veri kanalı için `50000–51000` aralığıdır. FTP her iki modda da kontrol ve veri olmak üzere iki kanal kullanır; SFTP tek SSH bağlantısı üzerinden çalışır.

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
    PROV --> OS{"İşletim sistemi"}
    OS -->|"Windows"| WU["WindowsLocalUserManager"]
    OS -->|"Docker/Linux"| LU["useradd + chpasswd + Linux izinleri"]
    PROV --> SS["ServerStorage"]
    FS --> FL["FluentFTP"]
    PROV --> SSHNET["SSH.NET doğrulama istemcisi"]
```

Bu diyagram, bir hata gördüğünüzde nereden başlamanız gerektiğini de söyler. Örneğin FileZilla parolayı kabul edip klasörü açamıyorsa `NgrokTunnelService` yerine `OpenSshSftpProvisioner` incelenir; Docker'da Linux sahiplik/chroot ayarları, yerel Windows'ta NTFS ACL katmanı kontrol edilir.
