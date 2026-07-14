# Docker Kurulum ve İşletim Rehberi

Docker çalışma modu, projeyi hosta .NET SDK, Node.js veya OpenSSH kurmadan çalıştırmanın önerilen yoludur. React production paketi Nginx container'ında; ASP.NET Core API, yerel FTP instance'ları, Linux OpenSSH/SFTP ve ngrok agent'ı backend container'ında çalışır.

## 1. Tek tıkla başlatma

1. Docker Desktop ve WSL2'yi kurun. WSL yoksa yönetici PowerShell'de `wsl --install` çalıştırıp bilgisayarı yeniden başlatın.
2. Proje kökündeki `Baslat.bat` dosyasına çift tıklayın.
3. İlk imaj derlemesinin tamamlanmasını bekleyin.
4. Açılan tarayıcıda `admin` / `admin123` ile giriş yapın ve parolayı değiştirin.

Başlatıcı Docker Desktop kapalıysa uygulamayı açmayı dener ve engine'in hazır olması için en fazla iki dakika bekler. İlk çalıştırmada boş portlar seçilir, `.docker/runtime.env` oluşturulur, imajlar sırayla derlenir, Compose servisleri başlatılır ve UI adresi açılır.

Terminal karşılığı:

```powershell
.\scripts\docker.ps1 start
```

## 2. Tekrar başlatınca ne olur?

`Baslat.bat` her çalıştırmada aynı klasör yolundan türetilen Compose proje adını ve aynı `.docker/runtime.env` dosyasını kullanır. Her seferinde yeni ve bağımsız bir proje yığını oluşturmaz.

- Kaynak veya imaj değişmediyse mevcut container çalışmaya devam eder.
- Backend ya da frontend imajı değiştiyse Compose yalnızca ilgili container'ı yenisiyle değiştirir.
- Container kimliği değişebilir; servis ve container adı aynı Compose projesi altında kalır.
- Named volume'ler container'dan bağımsız olduğu için veritabanı, loglar, dosyalar ve SSH anahtarları korunur.
- `.docker/runtime.env` silinmedikçe host portları aynı kalır.

## 3. Kod değişiklikleri otomatik yansır mı?

Hayır. Production Docker düzeninde kaynak klasörleri container'a bind mount edilmez ve hot reload yoktur. Kod, imaj oluşturulurken kopyalanır.

VS Code'da `.jsx`, `.js` veya `.cs` dosyası değiştirdikten sonra:

```powershell
.\Baslat.bat
```

komutunu yeniden çalıştırın. Yalnız tarayıcıyı yenilemek eski imajdaki kodu değiştirmez. `package.json`, `.csproj`, Dockerfile veya Compose değişikliklerinde de aynı yeniden derleme gerekir.

## 4. Port stratejisi

`scripts/docker.ps1`, bilgisayardaki aktif TCP dinleyicilerini okuyup `20000–60000` arasından şu kaynakları ayırır:

| Kaynak | Ayrılan port |
| --- | --- |
| Web arayüzü | 1 benzersiz port |
| API tanılama erişimi | 1 benzersiz port |
| SFTP | 1 benzersiz port |
| FTP kontrol bağlantıları | 10 portluk aralık |
| FTP PASV/EPSV veri kanalları | 50 portluk aralık |

Bütün eşlemeler varsayılan olarak yalnızca `127.0.0.1` üzerinde yayınlanır. Seçimler `.docker/runtime.env` içinde saklanır ve sonraki başlatmalarda aynen kullanılır. Farklı klasörlerdeki proje kopyaları, klasör yolundan üretilen ayrı Compose proje adları ve boş port taraması sayesinde birbirinden izole çalışabilir.

Yeni FTP sunucusu eklerken port alanını boş bırakabilirsiniz. Backend ayrılan 10 portluk Docker aralığındaki ilk kullanılmayan portu seçer. Elle port girilecekse değer aynı aralıkta ve diğer sunuculardan farklı olmalıdır.

Portları bilerek yeniden üretmek için önce stack'i durdurun, `.docker/runtime.env` dosyasını silin ve yeniden başlatın. LiteDB'de eski FTP portları kayıtlıysa bunları yeni aralığa taşımanız gerekir; normal kullanımda runtime dosyasını silmeyin.

## 5. Servis, ağ ve veri modeli

| Bileşen | Container içi adres/konum | Host erişimi veya kalıcılık |
| --- | --- | --- |
| Frontend/Nginx | `frontend:8080` | `127.0.0.1:<UI_PORT>` |
| ASP.NET Core API | `backend:8080` | Nginx `/api` proxy'si ve `127.0.0.1:<API_PORT>` |
| LiteDB ve loglar | `/app/logs` | `ftp_manager_logs` volume'ü |
| FTP/SFTP dosyaları | `/app/uploads` | `ftp_manager_uploads` volume'ü |
| OpenSSH anahtarları/yapılandırması | `/etc/ssh` | `ftp_manager_ssh` volume'ü |

Nginx `/api` isteklerini Docker ağındaki `backend:8080` adresine proxy eder. Tarayıcı UI ve API için aynı origin'i kullandığından sabit `localhost:5230` adresine veya geniş bir production CORS kuralına ihtiyaç yoktur.

`Durdur.bat` ve `docker.ps1 stop`, container'ları ve Compose ağını kaldırır fakat named volume'leri silmez. Volume kalıcılığı yedek değildir; önemli verileri ayrıca yedekleyin.

## 6. Yönetim komutları

```powershell
# Güncel kodla imajları derle ve servisleri başlat
.\scripts\docker.ps1 start

# Container ve health durumlarını gör
.\scripts\docker.ps1 status

# Canlı logları izle
.\scripts\docker.ps1 logs

# Container'ları kaldır; named volume'leri koru
.\scripts\docker.ps1 stop

# Çözülmüş Compose yapılandırmasını gör
.\scripts\docker.ps1 config
```

Daha ayrıntılı tanılama:

```powershell
docker compose --env-file .docker\runtime.env --file compose.yaml ps --all
docker compose --env-file .docker\runtime.env --file compose.yaml logs --tail 200 backend
Get-Content .\.docker\runtime.env
```

## 7. SFTP ve ngrok

Docker modunda her FTP sunucusu için chroot ile sınırlı bir Linux OpenSSH kullanıcısı oluşturulur. Kullanıcı yalnızca ilgili sunucunun `data` klasörüne yazabilir. OpenSSH backend container'ında çalıştığı için FTP ve SFTP aynı `ftp_manager_uploads` volume'ünü görür.

Ngrok agent'ı backend imajına dahildir. Başlatıcı token'ı şu sırayla arar:

1. Geçerli süreçteki `NGROK_AUTHTOKEN` environment değişkeni.
2. Standart ngrok config konumları.
3. Microsoft Store ngrok paketinin sanallaştırılmış config konumu.

Token mevcut ngrok config'inde kayıtlıysa `Baslat.bat` bunu otomatik bulup yalnızca başlatma sürecinde container'a aktarır; token ekrana, `.docker/runtime.env` dosyasına veya proje dosyalarına yazılmaz. Ngrok henüz yapılandırılmadıysa hostta bir kez çalıştırın:

```powershell
ngrok config add-authtoken <TOKEN>
.\scripts\docker.ps1 start
```

İsterseniz config yerine yalnız geçerli PowerShell oturumu için `$env:NGROK_AUTHTOKEN` tanımlayabilirsiniz. Değer container'a normal bir environment değişkeni olarak aktarılır; Docker secret nesnesi değildir. Token'ı `.docker/runtime.env`, `compose.yaml`, Git veya ekran görüntülerine yazmayın.

## 8. Git ve Docker build context sınırları

`.gitignore` şu yerel/üretilen içerikleri paylaşım dışında bırakır:

- `.docker/runtime.env`
- `Tests/`, `artifacts/` ve test dosyaları
- `bin/`, `obj/`, `dist/` ve `node_modules/`
- Backend log, veritabanı ve upload klasörleri

`.dockerignore` da `.docker/`, testler, bağımlılıklar, derleme çıktıları, yerel agent/IDE klasörleri, loglar ve upload'ları build context dışında tutar. Böylece yerel portlar, veritabanları, test çıktıları ve geliştirme dosyaları imaj katmanlarına kopyalanmaz.

## 9. Sorun giderme

- **Docker Desktop iki dakika içinde hazır olmadı:** Docker Desktop'ı açıp engine durumunu kontrol edin, ardından tekrar başlatın.
- **`port is already allocated`:** `.docker/runtime.env` içindeki bir port sonradan başka süreç tarafından alınmıştır. Önce çakışan süreci kapatın; kalıcı FTP kayıtları nedeniyle runtime dosyasını ilk çözüm olarak silmeyin.
- **Backend `unhealthy` veya durmuş:** `ps --all` ve backend loglarıyla gerçek başlangıç hatasını okuyun. Frontend sayfasını yenilemek backend'i başlatmaz.
- **Kod değişikliği görünmüyor:** `Baslat.bat` ile imajları yeniden derleyin; bu yapı hot reload kullanmaz.
- **FTP girişi var ama listeleme yok:** İstemcide pasif modu açın ve runtime dosyasındaki PASV port aralığını kontrol edin.
- **SFTP hazırlama başarısız:** Backend logunda `sshd -t`, Linux kullanıcı hesabı, chroot veya klasör sahipliği hatasını arayın.
- **Yetki ekranı oturum hatası gösteriyor:** Önce `/api/access/me`, `/users`, `/roles` ve `/permissions` durumlarını ayrı ayrı kontrol edin; tek endpoint'in iç hatasını genel oturum problemi sanmayın.

## 10. Güvenlik notları

- Varsayılan `admin123` parolasını ilk girişten sonra değiştirin.
- FTP parolaları şifresiz protokol üzerinden taşınabilir; güvenilmeyen ağlarda SFTP veya FTPS kullanın.
- Varsayılan host eşlemeleri yalnızca loopback'e açıktır; proje kendiliğinden LAN'a veya internete yayınlanmaz.
- LAN/public erişim için PASV adresi, NAT, firewall, DNS ve TLS/SFTP güvenliği ayrıca tasarlanmalıdır.
- Docker socket'i container'lara bağlanmaz; backend host Docker daemon'ını yönetemez.
- Named volume'lerin düzenli harici yedeğini alın.
