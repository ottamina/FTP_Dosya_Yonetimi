param(
    [ValidateSet('start', 'stop', 'status', 'logs', 'config')]
    [string]$Action = 'start'
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$RuntimeDirectory = Join-Path $Root '.docker'
$EnvFile = Join-Path $RuntimeDirectory 'runtime.env'
$ComposeFile = Join-Path $Root 'compose.yaml'

function Test-DockerDaemon {
    try {
        & docker info --format '{{.ServerVersion}}' *> $null
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Assert-WslAvailable {
    try {
        & wsl.exe --status *> $null
        if ($LASTEXITCODE -eq 0) { return }
    }
    catch { }

    throw 'Bu proje Linux container kullanir ancak WSL kurulu degil. Yonetici PowerShell acip "wsl --install" calistirin, bilgisayari yeniden baslatin ve Baslat.bat dosyasini tekrar acin.'
}

function Start-DockerDesktop {
    if (Test-DockerDaemon) { return }
    Assert-WslAvailable

    $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path -LiteralPath $desktop)) {
        throw 'Docker Desktop calismiyor ve standart kurulum yolunda bulunamadi.'
    }

    Write-Host 'Docker Desktop baslatiliyor...'
    Start-Process -FilePath $desktop -WindowStyle Hidden
    $deadline = (Get-Date).AddMinutes(2)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if (Test-DockerDaemon) { return }
    }
    throw 'Docker Desktop 2 dakika icinde hazir olmadi. Docker Desktop durumunu kontrol edin.'
}

function Get-UsedPorts {
    $ports = [System.Collections.Generic.HashSet[int]]::new()
    $properties = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
    foreach ($endpoint in $properties.GetActiveTcpListeners()) { [void]$ports.Add($endpoint.Port) }
    return $ports
}

function Find-FreeBlock {
    param(
        [System.Collections.Generic.HashSet[int]]$Used,
        [int]$Length,
        [int]$Minimum = 20000,
        [int]$Maximum = 60000
    )

    $candidateCount = $Maximum - $Minimum - $Length + 1
    $first = Get-Random -Minimum 0 -Maximum $candidateCount
    for ($index = 0; $index -lt $candidateCount; $index++) {
        $candidate = $Minimum + (($first + $index) % $candidateCount)
        $free = $true
        for ($offset = 0; $offset -lt $Length; $offset++) {
            if ($Used.Contains($candidate + $offset)) { $free = $false; break }
        }
        if ($free) {
            for ($offset = 0; $offset -lt $Length; $offset++) { [void]$Used.Add($candidate + $offset) }
            return $candidate
        }
    }
    throw "$Length adet ardisik bos port bulunamadi."
}

function New-RuntimeEnvironment {
    New-Item -ItemType Directory -Path $RuntimeDirectory -Force | Out-Null
    $used = Get-UsedPorts
    $uiPort = Find-FreeBlock -Used $used -Length 1
    $apiPort = Find-FreeBlock -Used $used -Length 1
    $sftpPort = Find-FreeBlock -Used $used -Length 1
    $ftpMin = Find-FreeBlock -Used $used -Length 10
    $passiveMin = Find-FreeBlock -Used $used -Length 50
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($Root.ToLowerInvariant()))
    }
    finally {
        $sha256.Dispose()
    }
    $projectSuffix = ([BitConverter]::ToString($hashBytes).Replace('-', '').Substring(0, 8)).ToLowerInvariant()

    @(
        "COMPOSE_PROJECT_NAME=ftp-manager-$projectSuffix"
        "UI_PORT=$uiPort"
        "API_PORT=$apiPort"
        "SFTP_PORT=$sftpPort"
        "FTP_PORT_MIN=$ftpMin"
        "FTP_PORT_MAX=$($ftpMin + 9)"
        "FTP_PASSIVE_PORT_MIN=$passiveMin"
        "FTP_PASSIVE_PORT_MAX=$($passiveMin + 49)"
    ) | Set-Content -LiteralPath $EnvFile -Encoding ascii
}

function Import-NgrokAuthtoken {
    if (-not [string]::IsNullOrWhiteSpace($env:NGROK_AUTHTOKEN)) {
        return $true
    }

    $configFiles = [System.Collections.Generic.List[string]]::new()
    $configFiles.Add((Join-Path $env:LOCALAPPDATA 'ngrok\ngrok.yml'))
    $configFiles.Add((Join-Path $env:USERPROFILE '.config\ngrok\ngrok.yml'))
    $configFiles.Add((Join-Path $env:USERPROFILE '.ngrok2\ngrok.yml'))

    $packageRoot = Join-Path $env:LOCALAPPDATA 'Packages'
    if (Test-Path -LiteralPath $packageRoot) {
        Get-ChildItem -LiteralPath $packageRoot -Directory -Filter 'ngrok.ngrok_*' -ErrorAction SilentlyContinue |
            ForEach-Object {
                $configFiles.Add((Join-Path $_.FullName 'LocalCache\Local\ngrok\ngrok.yml'))
            }
    }

    foreach ($configFile in $configFiles | Select-Object -Unique) {
        if (-not (Test-Path -LiteralPath $configFile)) { continue }

        $tokenLine = Get-Content -LiteralPath $configFile -ErrorAction Stop |
            Where-Object { $_ -match '^\s*authtoken\s*:\s*(.+?)\s*$' } |
            Select-Object -First 1
        if ($null -eq $tokenLine) { continue }

        $token = ([regex]::Match($tokenLine, '^\s*authtoken\s*:\s*(.+?)\s*$').Groups[1].Value).Trim()
        if (($token.StartsWith('"') -and $token.EndsWith('"')) -or
            ($token.StartsWith("'") -and $token.EndsWith("'"))) {
            $token = $token.Substring(1, $token.Length - 2)
        }
        if ([string]::IsNullOrWhiteSpace($token)) { continue }

        $env:NGROK_AUTHTOKEN = $token
        Write-Host "Ngrok authtoken mevcut yerel ngrok ayarindan guvenli sekilde yuklendi." -ForegroundColor Green
        return $true
    }

    Write-Warning 'Ngrok authtoken bulunamadi. Uygulama calisir ancak internet tuneli acilamaz. Once "ngrok config add-authtoken <TOKEN>" calistirin.'
    return $false
}

function Invoke-Compose {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & docker compose --env-file $EnvFile --file $ComposeFile @Arguments
    if ($LASTEXITCODE -ne 0) { throw "docker compose islemi basarisiz oldu: $($Arguments -join ' ')" }
}

Set-Location $Root

if ($Action -eq 'start') {
    Start-DockerDesktop
    if (-not (Test-Path -LiteralPath $EnvFile)) {
        New-RuntimeEnvironment
    }
    [void](Import-NgrokAuthtoken)
    # Compose v5 can fail while opening a shared Bake session for concurrent
    # builds. Build each local image separately, then start without rebuilding.
    Invoke-Compose build backend
    Invoke-Compose build frontend
    Invoke-Compose up --detach --no-build --remove-orphans
    $values = @{}
    Get-Content -LiteralPath $EnvFile | ForEach-Object {
        $key, $value = $_ -split '=', 2
        $values[$key] = $value
    }
    Write-Host ''
    Write-Host "FTP Manager hazir: http://localhost:$($values.UI_PORT)" -ForegroundColor Green
    Write-Host "API: http://localhost:$($values.API_PORT)"
    Write-Host "SFTP portu: $($values.SFTP_PORT)"
    Write-Host "FTP port araligi: $($values.FTP_PORT_MIN)-$($values.FTP_PORT_MAX)"
    Start-Process "http://localhost:$($values.UI_PORT)"
    exit 0
}

if (-not (Test-Path -LiteralPath $EnvFile)) {
    throw 'Runtime ayari bulunamadi. Once Baslat.bat veya scripts/docker.ps1 start calistirin.'
}

Start-DockerDesktop
switch ($Action) {
    'stop'   { Invoke-Compose down; Write-Host 'FTP Manager durduruldu. Kalici veriler korundu.' -ForegroundColor Yellow }
    'status' { Invoke-Compose ps }
    'logs'   { Invoke-Compose logs --follow --tail 200 }
    'config' { Invoke-Compose config }
}
