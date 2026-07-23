@echo off
setlocal
cd /d "%~dp0"
title FTP Dosya Yonetim Paneli - Durdur

docker compose stop
if errorlevel 1 (
  echo Uygulama durdurulamadi. Docker Desktop'un acik oldugunu kontrol edin.
  pause
  exit /b 1
)

echo Uygulama durduruldu. Veriler korunur.
timeout /t 3 /nobreak >nul
