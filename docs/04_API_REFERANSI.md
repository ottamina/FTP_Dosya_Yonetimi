# API Referansı

## 1. Temel adresler

| Çalışma modu | UI | API |
| --- | --- | --- |
| Docker | `http://localhost:<UI_PORT>` | Tarayıcıdan aynı origin üzerinde `/api`; doğrudan tanılama için `http://localhost:<API_PORT>/api` |
| Yerel geliştirme | `http://localhost:5173` | `http://localhost:5230/api` |

Docker'daki `UI_PORT` ve `API_PORT` değerleri `.docker/runtime.env` dosyasında bulunur. Frontend production kodu varsayılan olarak göreli `/api` kökünü kullanır; Nginx bunu Compose ağındaki `backend:8080` adresine yönlendirir.

## 2. Kimlik header'ları

```mermaid
flowchart LR
    Request["HTTP isteği"] --> App["Authorization: Bearer {appToken}"]
    Request --> Server["X-FTP-Server-Id"]
    Request --> User["X-FTP-Username"]
    Request --> Pass["X-FTP-Password"]
```

| Header | Amaç |
| --- | --- |
| `Authorization: Bearer <token>` | Uygulama kullanıcısı ve rol izinleri |
| `X-App-Token` | Bearer yerine desteklenen alternatif uygulama token header'ı |
| `X-FTP-Server-Id` | Yönetilen sunuculardan hangisinin seçildiği |
| `X-FTP-Username` | O FTP sunucusuna bağlanacak hesap |
| `X-FTP-Password` | O FTP sunucusunun parolası |
| `X-FTP-Host`, `X-FTP-Port` | Yönetilen sunucu kimliği verilmediğinde doğrudan uzak FTP hedefi |

## 3. Uygulama erişim endpoint'leri

| Metot | Yol | Görev | Gerekli izin |
| --- | --- | --- | --- |
| POST | `/api/access/login` | Uygulama oturumu aç | Yok |
| GET | `/api/access/me` | Güncel kullanıcı/izinleri getir | Geçerli token |
| GET | `/api/access/permissions` | İzin kataloğu | `access.manage` |
| GET | `/api/access/roles` | Rolleri listele | `access.manage` |
| POST | `/api/access/roles` | Rol oluştur | `access.manage` |
| PUT | `/api/access/roles/{id}` | Rol güncelle | `access.manage` |
| DELETE | `/api/access/roles/{id}` | Rol sil | `access.manage` |
| GET | `/api/access/users` | Kullanıcıları listele | `access.manage` |
| POST | `/api/access/users` | Kullanıcı oluştur | `access.manage` |
| PUT | `/api/access/users/{id}` | Kullanıcı güncelle | `access.manage` |
| DELETE | `/api/access/users/{id}` | Kullanıcı sil | `access.manage` |

## 4. Dosya endpoint'leri

| Metot | Yol | Parametre/gövde | Sonuç |
| --- | --- | --- | --- |
| GET | `/api/ftp/list` | Query: `path` | `FtpItemDto[]` |
| POST | `/api/ftp/upload` | Multipart: `file`, `currentPath` | Dosyayı doğrudan yükler |
| POST | `/api/ftp/upload-chunk` | Multipart: parça ve upload metadata | Parçayı kaydeder; son parçada birleştirir |
| POST | `/api/ftp/cancel-upload` | Query: `uploadId` | Geçici parçaları temizler |
| POST | `/api/ftp/rename` | Query: `sourcePath`, `targetPath` | Taşır veya yeniden adlandırır |
| GET | `/api/ftp/download` | Query: `remotePath` | `application/octet-stream` |
| DELETE | `/api/ftp/delete` | Query: `path`, `isFolder` | Dosya veya klasörü siler |
| POST | `/api/ftp/create-folder` | Query: `path` | Dizin oluşturur |
| POST | `/api/ftp/login` | Query: `username`, parola header'dan | FTP kimlik doğrulamasını test eder |

> Mevcut kodda bu endpoint'lerin tamamında backend seviyesinde `PermissionKeys.Files*` zorlaması yoktur. Üretim güvenliği için her endpoint'e uygun `RequirePermission` eklenmelidir.

## 5. Log endpoint'leri

| Metot | Yol | Kaynak |
| --- | --- | --- |
| GET | `/api/ftp/logs/file` | JSONL günlükleri |
| GET | `/api/ftp/logs/database` | Günlük LiteDB logları |

Mevcut arayüz `logs.view` izni yoksa bunları istemez; backend controller tarafında ayrıca zorunlu izin kontrolü eklenmesi önerilir.

## 6. FTP sunucu yönetimi

| Metot | Yol | Görev | Gerekli izin |
| --- | --- | --- | --- |
| GET | `/api/ftp/servers` | Sunucuları ve çalışma durumunu getir | `servers.view` |
| POST | `/api/ftp/servers` | Sunucu oluştur ve gerekirse başlat | `servers.manage` |
| DELETE | `/api/ftp/servers/{id}` | SFTP hesabı + instance + dosyaları kaldır | `servers.manage` |
| POST | `/api/ftp/servers/{id}/start` | FTP instance başlat | `servers.manage` |
| POST | `/api/ftp/servers/{id}/stop` | FTP instance durdur | `servers.manage` |
| POST | `/api/ftp/servers/{id}/sftp` | Kısıtlı SFTP hesabı hazırla | `servers.manage` |

`servers.credentials` izni olmayan kullanıcılarda FTP parolası boş, SFTP parolası `null` döndürülür.

## 7. ngrok/SFTP tünel endpoint'leri

| Metot | Yol | Görev | Gerekli izin |
| --- | --- | --- | --- |
| GET | `/api/ftp/sftp/tunnel?localPort={SFTP_PORT}` | Tünel durumunu keşfet | `servers.view` |
| POST | `/api/ftp/servers/{id}/sftp/tunnel/start` | Sunucunun SSH portuna ngrok TCP aç | `servers.manage` |
| POST | `/api/ftp/sftp/tunnel/stop?localPort={SFTP_PORT}` | Uygulamanın açtığı tüneli kapat | `servers.manage` |

## 8. Temel istek örnekleri

### Uygulama girişi

```http
POST {{API_ROOT}}/access/login
Content-Type: application/json

{
  "username": "admin",
  "password": "..."
}
```

### FTP kökünü listeleme

```http
GET {{API_ROOT}}/ftp/list?path=/
Authorization: Bearer APP_TOKEN
X-FTP-Server-Id: SERVER_ID
X-FTP-Username: FTP_USER
X-FTP-Password: FTP_PASSWORD
```

### Sunucu oluşturma

```http
POST {{API_ROOT}}/ftp/servers
Authorization: Bearer APP_TOKEN
Content-Type: application/json

{
  "name": "Arşiv",
  "host": "127.0.0.1",
  "port": 0,
  "username": "archive",
  "password": "...",
  "isActive": true
}
```

Docker modunda `port: 0` gönderildiğinde backend ayrılmış FTP port aralığındaki ilk boş portu atar. Yerel geliştirmede kullanılacak portu açıkça verin.

### SFTP hazırlama

```http
POST {{API_ROOT}}/ftp/servers/SERVER_ID/sftp
Authorization: Bearer APP_TOKEN
```

Bu örneklerde `API_ROOT`, Docker UI üzerinden çağrıda `/api`; Docker doğrudan tanılama portunda `http://localhost:<API_PORT>/api`; yerel geliştirmede `http://localhost:5230/api` değeridir.

## 9. Model ilişkileri

```mermaid
classDiagram
    class FtpServerConfig {
      string Id
      string Name
      string Host
      int Port
      string Username
      string Password
      bool IsActive
      bool IsRunning
      string SftpUsername
      string SftpPassword
      bool SftpEnabled
      int SftpLocalPort
    }
    class FtpItemDto {
      string Name
      string FullName
      bool IsFolder
      long Size
      DateTime Modified
    }
    class AppUser {
      string Id
      string Username
      string PasswordHash
      string RoleId
    }
    class AppRole {
      string Id
      string Name
      List Permissions
    }
    class UserSession {
      string Token
      string UserId
      DateTime ExpiresAt
    }
    class SftpTunnelStatus {
      bool IsRunning
      bool IsOwnedByApplication
      int LocalPort
      string PublicHost
      int PublicPort
    }
    AppUser --> AppRole : RoleId
    UserSession --> AppUser : UserId
```

## 10. HTTP hata davranışı

```mermaid
flowchart TD
    Call["Controller çağrısı"] --> Try["Servis işlemi"]
    Try -->|Başarılı| OK["200 OK"]
    Try -->|Geçersiz giriş/iş kuralı| Bad["400 Bad Request"]
    Try -->|Oturum/kimlik sorunu| Unauth["401 Unauthorized"]
    Try -->|Dosya/FTP beklenmeyen hata| Server["500 Internal Server Error"]
```
