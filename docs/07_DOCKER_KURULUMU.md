# Docker Kurulum ve Isletim Rehberi

Bu yapida frontend, ASP.NET Core API, yerel FTP sunuculari ve OpenSSH/SFTP ayni Compose projesi icinde calisir. Bilgisayara .NET SDK, Node.js veya OpenSSH kurmak gerekmez; yalnizca Docker Desktop gerekir.

## Tek tikla baslatma

1. Docker Desktop'i ve WSL2'yi kurun. WSL yoksa Yonetici PowerShell'de `wsl --install` calistirip bilgisayari yeniden baslatin.
2. Proje kokundeki `Baslat.bat` dosyasina cift tiklayin.
3. Ilk image derlemesinin tamamlanmasini bekleyin.
4. Acilan tarayicida `admin` / `admin123` ile giris yapin ve parolayi degistirin.

Baslatici Docker Desktop kapaliysa acmayi dener ve en fazla iki dakika hazir olmasini bekler. Ardindan servislere bos portlar atar, Compose stack'ini baslatir ve uygulama adresini acar.

## Port stratejisi

`scripts/docker.ps1`, bilgisayardaki aktif TCP dinleyicilerini okuyup `20000-60000` arasindan su kaynaklari ayirir:

| Kaynak | Ayrilan port |
| --- | --- |
| Web arayuzu | 1 benzersiz port |
| API tanilama erisimi | 1 benzersiz port, yalnizca `127.0.0.1` |
| SFTP | 1 benzersiz port |
| FTP sunuculari | 10 portluk aralik |
| FTP PASV/EPSV veri kanali | 50 portluk aralik |

Secim `.docker/runtime.env` icinde saklanir ve sonraki baslatmalarda aynen kullanilir. Bu sayede LiteDB'de kayitli FTP portlari ile Docker port eslemeleri degismez. Farkli klasorlerdeki proje kopyalari, klasor yolundan uretilen ayri Compose proje adlari ve bos port taramasi sayesinde birbirinden izole calisir.

Yeni FTP sunucusu eklerken port alanini bos birakabilirsiniz. Backend, ayrilan 10 portluk Docker araligindan kullanilmayan ilk portu atomik olarak secer. Elle port girilecekse arayuzdeki mevcut sunucularla cakismamali ve Docker FTP araligi icinde olmalidir.

Portlari bilerek yeniden uretmek icin once stack'i durdurun, `.docker/runtime.env` dosyasini silin ve yeniden baslatin. Kalici veritabaninda eski FTP portlari bulunuyorsa bu islemden sonra sunucu portlarini yeni araliga tasimak gerekir; normal kullanimda runtime dosyasini silmeyin.

## Servis ve veri modeli

| Bilesen | Container ici adres | Kalicilik |
| --- | --- | --- |
| Frontend/Nginx | `frontend:8080` | Image icinde statik dosyalar |
| ASP.NET Core API | `backend:8080` | Durumsuz uygulama sureci |
| LiteDB ve loglar | `/app/logs` | `ftp_manager_logs` volume'u |
| FTP/SFTP dosyalari | `/app/uploads` | `ftp_manager_uploads` volume'u |
| OpenSSH anahtarlari | `/etc/ssh` | `ftp_manager_ssh` volume'u |

Nginx `/api` isteklerini Docker agindaki backend'e proxy eder. Tarayici ayni origin'i kullandigi icin production ortaminda sabit `localhost:5230` adresine veya genis CORS iznine ihtiyac yoktur.

## Yonetim komutlari

```powershell
# Baslat veya guncel kodla yeniden derle
.\scripts\docker.ps1 start

# Container ve health durumlarini gor
.\scripts\docker.ps1 status

# Canli loglari izle
.\scripts\docker.ps1 logs

# Container'lari kaldir; volume'leri ve dosyalari koru
.\scripts\docker.ps1 stop

# Cozulmus Compose yapilandirmasini gor
.\scripts\docker.ps1 config
```

`Durdur.bat` da `stop` komutunun cift tiklanabilir karsiligidir. Veri volume'lerini silmek normal durdurma isleminin parcasi degildir.

## SFTP ve ngrok

Docker modunda her FTP sunucusu icin chroot ile sinirli Linux OpenSSH kullanicisi olusturulur. SFTP kullanicisi yalnizca ilgili sunucunun `data` klasorune yazabilir. OpenSSH ayni backend container'inda calistigi icin FTP ve SFTP ayni kalici dosya deposunu gorur.

Ngrok agent'i backend image'ina dahildir. Internet tuneli kullanilacaksa baslatmadan once PowerShell oturumunda token'i tanimlayin; Compose bu degeri container'a secret ortam degiskeni olarak aktarir:

```powershell
$env:NGROK_AUTHTOKEN = 'tokeninizi-buraya-yazin'
.\scripts\docker.ps1 start
```

Token'i `runtime.env`, Compose dosyasi veya Git icine yazmayin. TCP endpoint kullanimi ngrok hesap kosullarina tabidir.

## Sorun giderme

- `Docker Desktop 2 dakika icinde hazir olmadi`: Docker Desktop'i acip engine durumunu kontrol edin, sonra tekrar baslatin.
- `port is already allocated`: `.docker/runtime.env` icindeki bir port sonradan baska uygulama tarafindan alinmistir. Cakisan uygulamayi kapatin; kalici FTP kayitlari nedeniyle runtime dosyasini ilk cozum olarak silmeyin.
- Backend `unhealthy`: `.\scripts\docker.ps1 logs` ile API baslangic ve LiteDB hatalarini okuyun.
- FTP girisi var ama listeleme yok: istemcide pasif modu acin ve runtime dosyasindaki pasif port araliginin guvenlik duvari tarafindan engellenmedigini kontrol edin.
- SFTP hazirlama basarisiz: backend logunda `sshd -t`, Linux kullanicisi veya klasor sahipligi hatasini arayin.

## Guvenlik notlari

- Varsayilan `admin123` parolasini ilk giristen sonra degistirin.
- FTP parolalari sifresiz protokol uzerinden tasinabilir; guvenilmeyen aglarda SFTP kullanin.
- UI, API, FTP ve SFTP portlari varsayilan olarak yalnizca `127.0.0.1` adresine acilir; yerel proje kendiliginden LAN'a veya internete yayinlanmaz. LAN/public erisim ayri bir ag, PASV adresi, firewall ve guvenlik tasarimi gerektirir.
- Docker socket'i container'lara baglanmaz. Backend host Docker daemon'ini yonetemez.
