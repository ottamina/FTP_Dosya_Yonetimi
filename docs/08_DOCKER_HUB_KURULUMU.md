# Docker Hub ile kurulum

`compose.hub.yaml`, kaynak kodu derlemeden Docker Hub'daki sabit imajlari calistirir. Varsayilan etiket `53355c2`dir; bu etiket test edilmis ayni backend ve frontend surumunu sabitler.

Gizli degerleri `compose.hub.yaml` icine yazmayin. Yerel `.env` dosyasi Git tarafindan yok sayilir. Ngrok token'ini tercihen sadece baslatacaginiz PowerShell oturumunda `NGROK_AUTHTOKEN` olarak verin.

## Ilk calistirma

```powershell
Copy-Item .env.hub.example .env
docker compose -f compose.hub.yaml pull
docker compose -f compose.hub.yaml up -d
```

Arayuz varsayilan olarak `http://localhost:54936` adresindedir. Ilk hesap olusturulduktan sonra mevcut sistemi tasimak icin eski bilgisayardan indirdiginiz yedegi **FTP Sunucu Yonetimi > Yedekten geri yukle** adimiyla yukleyin.

## Guncelleme

Yeni bir sabit imaj etiketi duyuruldugunda `.env` icindeki `FTP_MANAGER_BACKEND_IMAGE` ve `FTP_MANAGER_FRONTEND_IMAGE` degerlerini o etikete ayarlayin; sonra `pull` ve `up -d` komutlarini tekrar calistirin.
