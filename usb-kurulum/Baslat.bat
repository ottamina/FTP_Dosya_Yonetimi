@echo off
setlocal
cd /d "%~dp0"
title FTP Dosya Yonetim Paneli - Baslat

where docker >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop bulunamadi. Once Docker Desktop'u kurup acin.
  pause
  exit /b 1
)

docker info >nul 2>nul
if errorlevel 1 (
  echo Docker Desktop calismiyor. Acilip hazir olmasini bekleyin, sonra tekrar deneyin.
  pause
  exit /b 1
)

if not exist "ftp-manager-images.tar" (
  echo ftp-manager-images.tar bulunamadi. USB paketi eksik.
  pause
  exit /b 1
)

echo Yerel uygulama imajlari yukleniyor...
docker load -i "ftp-manager-images.tar"
if errorlevel 1 (
  echo Yerel imaj arsivi yuklenemedi.
  pause
  exit /b 1
)

echo Uygulama baslatiliyor...
docker compose up -d
if errorlevel 1 (
  echo Uygulama baslatilamadi. Docker Desktop'u ve port cakismalarini kontrol edin.
  pause
  exit /b 1
)

echo Panel hazirlaniyor...
timeout /t 5 /nobreak >nul
start "" "http://localhost:54936"
echo Panel tarayicida acildi.
exit /b 0
