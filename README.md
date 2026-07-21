# FTP Dosya Yönetimi

> Ayrıntılı mimari, bütün servislerin açıklaması, FTP–SFTP–ngrok akışları, API referansı ve çok sayıda diyagram için [Eğitim ve Teknik Dokümantasyon Merkezi](docs/README.md) belgesinden başlayın.

Tarayıcı üzerinden FTP sunucularını yönetmek ve dosya işlemlerini yapmak için geliştirilmiş, tam yığın bir uygulamadır. React tabanlı arayüz; ASP.NET Core API, FluentFTP ile uzak sunucu bağlantıları ve yerleşik bir yerel FTP sunucusuyla birlikte çalışır.

## Öne çıkanlar

- Birden fazla FTP sunucusunu ekleme, listeleme, başlatma, durdurma ve silme
- Yerel FTP sunucusu oluşturma ve yönetme
- Dosya ve klasör listeleme, klasör oluşturma, indirme, silme, taşıma ve yeniden adlandırma
- Küçük dosyalar için doğrudan, 30 MB üzerindeki dosyalar için uyarlanabilir parçalı yükleme
- Metin, CSV, görsel ve PDF dosyaları için önizleme
- Dosya ağacında arama, sürükle-bırak yükleme ve yükleme ilerleme takibi
- Dosya işlemleri ve uygulama olayları için LiteDB ve JSON tabanlı loglar
- Oturum, kullanıcı, rol ve izin yönetimi
- Windows'ta yerel OpenSSH, Docker'da Linux OpenSSH ile sunucuya özel ve klasöre kısıtlı SFTP hesabı
- Ngrok TCP tünelini arayüzden başlatma, durdurma ve dış bağlantı adresini görme

## Teknolojiler

| Katman | Teknoloji |
| --- | --- |
| Arayüz | React 19, Vite, Axios, Tailwind CSS |
| API | ASP.NET Core (.NET 10) |
| FTP istemcisi | FluentFTP |
| Yerel FTP sunucusu | TcpListener tabanlı yerleşik sunucu |
| Kalıcılık | LiteDB |

## Proje yapısı

```text
.
├── Backend/
│   └── FtpManager.Api/
│       ├── Controllers/     # FTP ve erişim yönetimi HTTP uç noktaları
│       ├── Models/          # DTO, kullanıcı, rol ve FTP sunucusu modelleri
│       ├── Services/        # FTP, yerel sunucu, log ve erişim servisleri
│       └── Properties/      # Yerel geliştirme profilleri
├── Frontend/
│   ├── src/components/      # Dosya gezgini ve yönetim ekranları
│   └── src/services/        # API istemcisi
├── compose.yaml             # Frontend, API, FTP ve SFTP Docker servis tanımı
├── Baslat.bat               # Docker ortamını derler ve başlatır
├── Durdur.bat               # Container'ları kaldırır, volume'leri korur
├── scripts/docker.ps1       # Port seçimi ve Compose yaşam döngüsü
├── docs/                    # Mimari, API, işletim ve Docker rehberleri
└── README.md
```

## Gereksinimler

Önerilen Docker çalışma modu için:

- Docker Desktop ve WSL2

Yerel geliştirme modu için:

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Node.js 20 veya üzeri ve npm
- SFTP kullanılacaksa Windows OpenSSH Server ve yönetici yetkisi

Her iki modda da uzak bir FTP sunucusu isteğe bağlıdır; uygulama kendi yerel FTP sunucusunu oluşturabilir. İnternet tüneli kullanılacaksa ayrıca bir ngrok authtoken gerekir.

## Hızlı başlangıç

### Docker ile tek tıklama (önerilen)

Windows'ta Docker Desktop kuruluysa proje kökündeki `Baslat.bat` dosyasına çift tıklayın. İlk çalıştırmada imajlar derlenir, boş portlar otomatik seçilir, servisler arka planda başlatılır ve tarayıcı açılır. Veritabanı, loglar, FTP dosyaları ve OpenSSH anahtarları Docker volume'lerinde kalıcı tutulur.

Aynı işlem terminalden de çalıştırılabilir:

```powershell
.\scripts\docker.ps1 start
```

Yardımcı komutlar:

```powershell
.\scripts\docker.ps1 status
.\scripts\docker.ps1 logs
.\scripts\docker.ps1 stop
```

UI, API, SFTP, FTP kontrol ve FTP pasif veri portları ilk başlatmada birlikte ve çakışmayacak şekilde seçilir. Seçimler `.docker/runtime.env` dosyasında yerel olarak saklanır; dosya Git'e ve Docker build context'ine eklenmez. Kaynak kod değiştiğinde `Baslat.bat` imajları yeniden derler ve yalnızca değişen servislerin container'larını yeniler; kalıcı volume'ler korunur. Ayrıntılar için [Docker Kurulum ve İşletim Rehberi](docs/07_DOCKER_KURULUMU.md) belgesine bakın.

### Docker Hub veya USB paketi ile kurulum

Kaynak kodu derlemeden çalıştırmak için `compose.hub.yaml` kullanılabilir. `usb-kurulum/` klasörünü uygulama paketiyle birlikte USB'ye kopyalayın; hedef bilgisayarda Docker Desktop açıkken `usb-kurulum/Baslat.bat` dosyasını çalıştırın. Yerel `ftp-manager-images.tar` arşivi varsa imajlar internetsiz yüklenir; yoksa betik Docker Hub'dan imajları indirir. Ayrıntılı seçenekler ve ortam değişkenleri için [Docker Hub Kurulum Rehberi](docs/08_DOCKER_HUB_KURULUMU.md) belgesine bakın.

### Yerel geliştirme

İki terminal açın ve aşağıdaki komutları proje kök dizininden çalıştırın.

#### 1. API'yi başlatın

```powershell
dotnet run --project .\Backend\FtpManager.Api
```

Geliştirme profili API'yi varsayılan olarak `http://localhost:5230` adresinde başlatır.

#### 2. Arayüzü başlatın

```powershell
cd .\Frontend
npm install
$env:VITE_API_ROOT = 'http://localhost:5230/api'
npm run dev
```

Vite tarafından gösterilen adresi açın; varsayılan adres genellikle `http://localhost:5173` olur. API, bu kökenden gelen istekler için CORS yapılandırmasına sahiptir.

#### 3. Uygulamaya giriş yapın

İlk çalıştırmada aşağıdaki varsayılan yönetici hesabı oluşturulur:

| Kullanıcı adı | Parola |
| --- | --- |
| `admin` | `admin123` |

İlk girişten sonra parolayı değiştirmeniz ve üretimde varsayılan hesabı kullanmamanız önerilir.

## Kullanım akışı

1. Uygulama hesabınızla oturum açın.
2. **Sunucular** ekranından bir FTP sunucusu ekleyin veya yerel bir sunucu oluşturun.
3. Dosya gezgininde sunucuyu seçip FTP kullanıcı bilgileriyle bağlantıyı doğrulayın.
4. Klasörleri görüntüleyin; dosya yükleyin, indirin, önizleyin, taşıyın veya silin.
5. Büyük dosyalar 30 MB eşiğinin üzerinde parçalı yüklenir; 30–99 MB için 5 MB, 100–199 MB için 10 MB, 200 MB ve üzeri için 20 MB parçalar kullanılır.
6. Yetkili kullanıcılar, **Erişim Yönetimi** ekranında kullanıcı, rol ve izinleri düzenleyebilir.

## Erişim ve izinler

Uygulama erişim verisini `Backend/FtpManager.Api/logs/database/ftp_manager.db` altında LiteDB ile tutar. Oturumlar 12 saat geçerlidir ve istemci tarafından `Authorization: Bearer <token>` veya `X-App-Token` başlığıyla iletilir.

Yerleşik izinler şunları kapsar:

- Dosyaları görüntüleme, yükleme, indirme ve değiştirme
- FTP sunucularını görüntüleme, yönetme ve kimlik bilgilerini görüntüleme
- Logları görüntüleme
- Kullanıcı ve rol yönetimi

## API özeti

Docker modunda tarayıcı API'ye UI ile aynı origin üzerindeki `/api` yolundan erişir; Nginx isteği `backend:8080` adresine proxy eder. Yerel geliştirmede doğrudan API kökü `http://localhost:5230/api` olur. Docker'ın hosta açtığı tanılama portu `.docker/runtime.env` içindeki `API_PORT` değeridir.

| Alan | Bazı uç noktalar |
| --- | --- |
| FTP işlemleri | `GET /ftp/list`, `POST /ftp/upload`, `POST /ftp/upload-chunk`, `GET /ftp/download`, `POST /ftp/rename`, `DELETE /ftp/delete` |
| Klasör ve loglar | `POST /ftp/create-folder`, `GET /ftp/logs/file`, `GET /ftp/logs/database` |
| Sunucular | `GET/POST /ftp/servers`, `POST /ftp/servers/{id}/start`, `POST /ftp/servers/{id}/stop` |
| SFTP ve ngrok | `POST /ftp/servers/{id}/sftp`, `GET /ftp/sftp/tunnel`, `POST /ftp/servers/{id}/sftp/tunnel/start`, `POST /ftp/sftp/tunnel/stop` |
| Erişim yönetimi | `POST /access/login`, `GET /access/me`, kullanıcı ve rol CRUD uç noktaları |

FTP istekleri, seçilen sunucu ve bağlanacak FTP hesabı için `X-FTP-Server-Id`, `X-FTP-Username` ve `X-FTP-Password` başlıklarını kullanır.

## SFTP ve ngrok kullanımı

SFTP hazırlama işletim sistemine göre iki farklı yol izler:

- **Docker:** Backend container'ı Linux kullanıcısını, chroot klasörünü ve container içindeki OpenSSH yapılandırmasını yönetir. Hostta OpenSSH kurulumu veya yönetici PowerShell'i gerekmez.
- **Yerel Windows:** API, Windows hesabı, NTFS izinleri ve Windows OpenSSH yapılandırmasını yönettiği için **Yönetici olarak** çalıştırılmalıdır.

Her iki modda da her sunucu için ayrı bir SFTP kullanıcısı oluşturulur ve kullanıcı yalnızca ilgili sunucunun `data` klasörüne yazabilir. Yapılandırma `sshd -t` ile doğrulanmadan SSH servisi devreye alınmaz.

Ngrok kullanmadan SFTP bağlantısı yerel olarak `127.0.0.1:<OpenSSH portu>` adresinden yapılır. İnternetten geçici erişim gerektiğinde:

1. Bir ngrok hesabı oluşturup hostta bir kez `ngrok config add-authtoken <TOKEN>` çalıştırın. Docker başlatıcısı standart ve Microsoft Store config konumlarındaki kayıtlı token'ı otomatik olarak container'a aktarır.
2. Sunucu kartında **Kısıtlı SFTP erişimini hazırla** düğmesini kullanın.
3. **İnternet tünelini aç** düğmesine basın.
4. Arayüzde gösterilen ngrok host ve portunu, sunucu kartındaki SFTP kullanıcı adı ve parolasıyla kullanın.

Ngrok adresi geçici olabilir. Tünel kapandığında internetten erişim de kapanır; yerel SFTP ve FTP dosyaları silinmez.

## Geliştirme komutları

```powershell
# Docker imajlarını güncel kodla yeniden derle ve servisleri başlat
.\scripts\docker.ps1 start

# Docker servis durumunu gör
.\scripts\docker.ps1 status

# Arayüzü üretim için derle
cd .\Frontend
npm run build

# Arayüz kod kalitesini denetle
npm run lint

# API'yi derle
dotnet build .\Backend\FtpManager.Api
```

## Güvenlik notları

- FTP şifreleri ve uygulama veritabanı yerel geliştirme verisi olarak değerlendirilmelidir; depoya eklenmemelidir.
- `logs/`, `uploads/`, `.docker/runtime.env`, derleme klasörleri ve test dosyaları `.gitignore` ile dışarıda bırakılır; aynı yerel içerikler `.dockerignore` ile imajlara da alınmaz.
- Docker volume'leri kalıcıdır ancak yedek değildir; önemli veriler ayrıca yedeklenmelidir.
- FTP, şifreleri düz metinle iletebilir. İnternet üzerinden kullanım için FTPS/SFTP ve uygun ağ güvenliği tercih edilmelidir.
- Log ekranı performans için en yeni 500 kaydı gösterir; **Yenile** düğmesi aktif sekmeyi yeniden yükler.

## Lisans

Bu proje için henüz bir lisans tanımlanmamıştır. Dağıtım veya açık kaynak kullanımı öncesinde uygun bir lisans ekleyin.
