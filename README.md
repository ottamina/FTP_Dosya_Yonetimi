# FTP Dosya Yönetimi

Tarayıcı üzerinden FTP sunucularını yönetmek ve dosya işlemlerini yapmak için geliştirilmiş, tam yığın bir uygulamadır. React tabanlı arayüz; ASP.NET Core API, FluentFTP ile uzak sunucu bağlantıları ve yerleşik bir yerel FTP sunucusuyla birlikte çalışır.

## Öne çıkanlar

- Birden fazla FTP sunucusunu ekleme, listeleme, başlatma, durdurma ve silme
- Yerel FTP sunucusu oluşturma ve yönetme
- Dosya ve klasör listeleme, klasör oluşturma, indirme, silme, taşıma ve yeniden adlandırma
- Küçük dosyalar için doğrudan, büyük dosyalar için parçalara bölünmüş yükleme
- Metin, CSV, görsel ve PDF dosyaları için önizleme
- Dosya ağacında arama, sürükle-bırak yükleme ve yükleme ilerleme takibi
- Dosya işlemleri ve uygulama olayları için LiteDB ve JSON tabanlı loglar
- Oturum, kullanıcı, rol ve izin yönetimi

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
└── README.md
```

## Gereksinimler

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Node.js 20 veya üzeri ve npm
- Uzak bir FTP sunucusu (isteğe bağlı; uygulama yerel sunucu da oluşturabilir)

## Hızlı başlangıç

İki terminal açın ve aşağıdaki komutları proje kök dizininden çalıştırın.

### 1. API'yi başlatın

```powershell
dotnet run --project .\Backend\FtpManager.Api
```

Geliştirme profili API'yi varsayılan olarak `http://localhost:5230` adresinde başlatır.

### 2. Arayüzü başlatın

```powershell
cd .\Frontend
npm install
npm run dev
```

Vite tarafından gösterilen adresi açın; varsayılan adres genellikle `http://localhost:5173` olur. API, bu kökenden gelen istekler için CORS yapılandırmasına sahiptir.

### 3. Uygulamaya giriş yapın

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
5. Yetkili kullanıcılar, **Erişim Yönetimi** ekranında kullanıcı, rol ve izinleri düzenleyebilir.

## Erişim ve izinler

Uygulama erişim verisini `Backend/FtpManager.Api/logs/database/ftp_manager.db` altında LiteDB ile tutar. Oturumlar 12 saat geçerlidir ve istemci tarafından `Authorization: Bearer <token>` veya `X-App-Token` başlığıyla iletilir.

Yerleşik izinler şunları kapsar:

- Dosyaları görüntüleme, yükleme, indirme ve değiştirme
- FTP sunucularını görüntüleme, yönetme ve kimlik bilgilerini görüntüleme
- Logları görüntüleme
- Kullanıcı ve rol yönetimi

## API özeti

API kök adresi: `http://localhost:5230/api`

| Alan | Bazı uç noktalar |
| --- | --- |
| FTP işlemleri | `GET /ftp/list`, `POST /ftp/upload`, `POST /ftp/upload-chunk`, `GET /ftp/download`, `POST /ftp/rename`, `DELETE /ftp/delete` |
| Klasör ve loglar | `POST /ftp/create-folder`, `GET /ftp/logs/file`, `GET /ftp/logs/database` |
| Sunucular | `GET/POST /ftp/servers`, `POST /ftp/servers/{id}/start`, `POST /ftp/servers/{id}/stop` |
| Erişim yönetimi | `POST /access/login`, `GET /access/me`, kullanıcı ve rol CRUD uç noktaları |

FTP istekleri, seçilen sunucu ve bağlanacak FTP hesabı için `X-FTP-Server-Id`, `X-FTP-Username` ve `X-FTP-Password` başlıklarını kullanır.

## Geliştirme komutları

```powershell
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
- `logs/`, `uploads/`, derleme klasörleri ve test dosyaları `.gitignore` ile dışarıda bırakılır.
- FTP, şifreleri düz metinle iletebilir. İnternet üzerinden kullanım için FTPS/SFTP ve uygun ağ güvenliği tercih edilmelidir.

## Lisans

Bu proje için henüz bir lisans tanımlanmamıştır. Dağıtım veya açık kaynak kullanımı öncesinde uygun bir lisans ekleyin.
