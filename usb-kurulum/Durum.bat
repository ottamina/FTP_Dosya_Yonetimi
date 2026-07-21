@echo off
setlocal
cd /d "%~dp0"
title FTP Dosya Yonetim Paneli - Durum

docker compose -f compose.hub.yaml ps
echo.
echo Tarayicida acmak icin: http://localhost:54936
pause
