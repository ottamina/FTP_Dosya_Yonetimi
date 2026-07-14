# Akışlar ve Protokoller

## 1. Uygulama girişi

```mermaid
sequenceDiagram
    actor U as Kullanıcı
    participant R as React
    participant AC as AccessController
    participant AS as AccessService
    participant DB as LiteDB

    U->>R: Kullanıcı adı + parola
    R->>AC: POST /api/access/login
    AC->>AS: Login
    AS->>DB: AppUser kaydını oku
    AS->>AS: PBKDF2 doğrula
    AS->>DB: UserSession oluştur
    AS-->>AC: token + kullanıcı + izinler
    AC-->>R: 200 OK
    R->>R: token ve kullanıcıyı localStorage'a yaz
```

Bu giriş, FTP veya SFTP girişi değildir. Yalnızca web uygulamasına kim olduğunuzu söyler.

## 2. FTP bağlantısını doğrulama ve klasör listeleme

```mermaid
sequenceDiagram
    actor U as Kullanıcı
    participant UI as React Sidebar
    participant API as FtpController
    participant FS as FtpService
    participant FTP as FtpServerInstance
    participant DISK as data klasörü

    U->>UI: FTP kullanıcı/parola gir
    UI->>API: POST /ftp/login + X-FTP-* header'ları
    API->>FS: VerifyCredentialsAsync
    FS->>FTP: USER / PASS
    FTP-->>FS: 230 User logged in
    FS-->>API: true
    API-->>UI: Giriş başarılı
    UI->>API: GET /ftp/list?path=/
    API->>FS: GetListAsync
    FS->>FTP: MLSD veya LIST
    FTP->>DISK: Dizini oku
    DISK-->>FTP: Dosya listesi
    FTP-->>FS: FTP data channel
    FS-->>UI: FtpItemDto[]
```

## 3. FTP neden iki bağlantı kullanır?

```mermaid
flowchart LR
    Client["FTP istemcisi"] -->|"Kontrol: USER, PASS, LIST"| Control["Yerel varsayılan veya Docker FTP port aralığı"]
    Client -->|"Veri: liste veya dosya baytları"| Passive["Yerel veya Docker PASV port aralığı"]
    Control --> Server["FtpServerInstance"]
    Passive --> Server
```

Yerel geliştirme varsayılanında kontrol portları `2121` ve devamı, pasif aralık `50000–51000` olur. Docker bu iki aralığı ilk başlangıçta boş host portlarından seçip `.docker/runtime.env` dosyasına kaydeder. Komut bağlantısı açık olsa bile pasif veri portları engellenirse giriş başarılı olur fakat listeleme/yükleme başarısız olur.

## 4. Küçük dosya yükleme

```mermaid
sequenceDiagram
    actor U as Kullanıcı
    participant UI as UploadPanel/App
    participant FC as FtpController
    participant FS as FtpService
    participant FTP as Yerel FTP
    participant DATA as data klasörü

    U->>UI: Dosya seç ve gönder
    UI->>FC: multipart POST /upload
    FC->>FC: currentPath + fileName
    FC->>FS: UploadFileAsync(stream, remotePath)
    FS->>FTP: STOR remotePath
    FTP->>DATA: FileStream oluştur/yaz
    DATA-->>FTP: Tamam
    FTP-->>FS: 226 Transfer complete
    FS-->>UI: Başarılı
    UI->>FC: Hedef klasörü yeniden listele
```

## 5. Büyük dosyada parçalı yükleme

```mermaid
flowchart TD
    File["Büyük dosya"] --> Split["Tarayıcıda CHUNK_SIZE parçalarına böl"]
    Split --> Loop["Her parçayı sırayla /upload-chunk'a gönder"]
    Loop --> Temp["uploads/temp/{uploadId}/{index}.part"]
    Temp --> Complete{"Tüm parçalar geldi mi?"}
    Complete -->|Hayır| Wait["Sonraki parçayı bekle"]
    Complete -->|Evet| Merge["merged.tmp oluştur"]
    Merge --> FtpUpload["Tek akış olarak FTP STOR"]
    FtpUpload --> Cleanup["Parçaları ve temp dosyayı sil"]
```

İptal veya hata durumunda `/cancel-upload`, ilgili geçici dosyaları temizler.

## 6. Seçilen yolun yükleme hedefine dönüşmesi

```mermaid
flowchart TD
    Selected["selectedPath"] --> Find["getSelectedItem"]
    Find --> Type{"Seçim klasör mü?"}
    Type -->|Evet| Same["Seçilen klasörü hedefle"]
    Type -->|Hayır| Parent["Dosyanın parent klasörünü hedefle"]
    Same --> Upload["remotePath = hedef + dosya adı"]
    Parent --> Upload
```

Bu kural, seçilmiş bir dosyanın yanlışlıkla klasör gibi kullanılmasını ve `dosya.pdf/yeni.txt` benzeri imkânsız yolları önler.

## 7. Taşıma ve yeniden adlandırma

```mermaid
sequenceDiagram
    participant UI as React
    participant API as FtpController
    participant FS as FtpService
    participant FTP as FTP sunucusu

    UI->>API: POST /rename?sourcePath&targetPath
    API->>FS: RenameAsync
    FS->>FTP: RNFR sourcePath
    FTP-->>FS: 350 Pending
    FS->>FTP: RNTO targetPath
    FTP-->>FS: 250 Completed
    FS-->>UI: Başarılı
```

Sürükle-bırak taşıma ve manuel yeniden adlandırma aynı altyapıyı kullanır; yalnızca hedef yolun hesaplanması farklıdır.

## 8. Dosya önizleme

```mermaid
flowchart LR
    Click["Dosyaya tıkla"] --> Ext{"Uzantı"}
    Ext -->|txt/json/xml/js/css/html/log| Text["Blob.text"]
    Ext -->|csv| Csv["Satır/sütun tablosu"]
    Ext -->|png/jpg/gif/svg| Image["Object URL + img"]
    Ext -->|pdf| Pdf["Object URL + iframe"]
    Ext -->|diğer| Unsupported["Önizleme desteklenmiyor"]
```

Önizleme de dosyayı FTP'den indirir. Görsel/PDF için oluşturulan object URL, panel kapanınca `URL.revokeObjectURL` ile temizlenir.

## 9. SFTP hazırlama

```mermaid
sequenceDiagram
    actor A as Yönetici
    participant UI as ServerManager
    participant API as FtpController
    participant LFS as LocalFtpServer
    participant P as OpenSshSftpProvisioner
    participant OS as Linux/Windows hesap ve izin katmanı
    participant SSH as sshd
    participant TEST as SSH.NET

    A->>UI: Kısıtlı SFTP erişimini hazırla
    UI->>API: POST /servers/{id}/sftp
    API->>LFS: ProvisionSftp
    LFS->>P: Provision(chroot, data)
    alt Docker/Linux
        P->>OS: useradd/chpasswd ve chown/chmod
    else Yerel Windows
        P->>OS: NetAPI hesabı ve NTFS ACL'leri
    end
    P->>SSH: Match User bloğunu yaz
    P->>SSH: sshd -t
    P->>SSH: Container sshd veya Windows sshd servisini yeniden başlat
    P->>TEST: 127.0.0.1 üzerinden bağlan ve listele
    TEST-->>P: SFTP alt sistemi çalışıyor
    P-->>UI: Kullanıcı, parola, port ve Hazır durumu
```

### Chroot izin resmi

```mermaid
flowchart TB
    MODE{"Çalışma modu"}
    MODE -->|"Docker"| P["/app/uploads/ftp_root"]
    MODE -->|"Windows"| W["C:/ProgramData/FtpManager/ftp_root"]
    P --> J["{serverId} chroot\nroot sahipliğinde, yazılamaz"]
    W --> J2["{serverId} chroot\nNTFS ile yazılamaz"]
    J --> D["data\nSFTP kullanıcısı yazabilir"]
    J2 --> D2["data\nSFTP kullanıcısı yazabilir"]
```

Bu nedenle FileZilla ile `/readme.txt` yüklemek reddedilir; doğru yazılabilir hedef `/data/readme.txt` olur.

## 10. ngrok dış erişimi

```mermaid
sequenceDiagram
    actor A as Yönetici
    participant UI as React
    participant API as NgrokTunnelService
    participant SSH as Yapılandırılmış yerel SFTP portu
    participant NG as ngrok cloud
    participant FZ as Uzak FileZilla

    A->>UI: İnternet tünelini aç
    UI->>API: POST tunnel/start
    API->>SSH: Port dinleniyor mu?
    SSH-->>API: Evet
    API->>NG: ngrok tcp 127.0.0.1:{SFTP_PORT}
    API->>API: 127.0.0.1:4040/api/tunnels
    API-->>UI: publicHost + publicPort
    FZ->>NG: SFTP bağlantısı
    NG->>SSH: Ham TCP'yi yönlendir
```

ngrok adresi bir web adresi gibi görünse de bağlantı protokolü HTTP değil, TCP üzerinden SSH/SFTP'dir.

## 11. FTP ve SFTP aynı dosyayı nasıl görür?

```mermaid
flowchart LR
    UI["Web arayüzünde /"] --> FTP["FtpServerInstance._ftpRoot"]
    FTP --> DATA["Docker volume veya ProgramData/{serverId}/data"]
    FZ["FileZilla'da /data"] --> SSH["OpenSSH chroot"]
    SSH --> DATA
```

| İşlem | Sonuç |
| --- | --- |
| Arayüzden `/rapor.pdf` yükle | FileZilla `/data/rapor.pdf` görür |
| FileZilla `/data/resim.png` yükle | Arayüz `/resim.png` görür |
| FileZilla `/resim.png` yüklemeye çalış | Chroot kökü yazılamadığı için izin hatası |

## 12. Log akışı

```mermaid
sequenceDiagram
    participant S as Servis/Controller
    participant L as LogService
    participant D as Günlük LiteDB
    participant J as JSONL
    participant T as Metin log
    participant UI as LogViewer

    S->>L: LogInfo/Warning/Error
    par Üç formata yaz
        L->>D: LogEntry insert
        L->>J: Tek JSON satırı append
        L->>T: Okunabilir satır append
    end
    UI->>L: logs/database veya logs/file
    L-->>UI: En yeniden eskiye kayıtlar
```
