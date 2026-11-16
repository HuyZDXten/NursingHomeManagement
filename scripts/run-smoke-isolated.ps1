<#
.SYNOPSIS
  Chay smoke test end-to-end CO LAP: khong dung database/API/FE dang chay de dev.

.DESCRIPTION
  1. Dung lai database NursingHomeDB_Test tu Database/*.sql
  2. Build API ra thu muc tam (khong bi khoa boi API dang chay o cong 5080)
  3. Chay PayOS gia lap o cong 5198 va API o cong 5081 (Jwt key ngau nhien, khong can appsettings.Development.json)
  4. node scripts/smoke-test.mjs, roi tat 2 tien trinh tren
  Exit code = ket qua smoke test (0 = tat ca PASS).

.EXAMPLE
  .\scripts\run-smoke-isolated.ps1
  .\scripts\run-smoke-isolated.ps1 -Server ".\SQLEXPRESS" -SqlUser sa -SqlPassword "..."
#>
param(
    [string]$Server = "localhost",
    [string]$SqlUser,
    [string]$SqlPassword,
    [int]$ApiPort = 5081,
    [int]$PayOsPort = 5198
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$database = "NursingHomeDB_Test"
$out = Join-Path ([System.IO.Path]::GetTempPath()) "nursinghome-smoke-api"
$api = "http://localhost:$ApiPort"
$fake = "http://localhost:$PayOsPort"

# 1. Database test
$dbArgs = @{ Server = $Server; Database = $database; Recreate = $true }
if ($SqlUser) { $dbArgs.SqlUser = $SqlUser; $dbArgs.SqlPassword = $SqlPassword }
& (Join-Path $root "Database\setup-local-db.ps1") @dbArgs | Out-Null

# 2. Build API ra thu muc tam
Write-Host ">> Build API -> $out" -ForegroundColor Cyan
dotnet build (Join-Path $root "BE\NursingHome.API\NursingHome.API.csproj") -o $out -v q -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build API that bai." }

$connection = if ($SqlUser) {
    "Server=$Server;Database=$database;User ID=$SqlUser;Password=$SqlPassword;TrustServerCertificate=True;MultipleActiveResultSets=true"
} else {
    "Server=$Server;Database=$database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
$jwtKey = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))

$saved = @{}
function Set-TempEnv([hashtable]$vars) {
    foreach ($k in $vars.Keys) {
        $saved[$k] = [Environment]::GetEnvironmentVariable($k)
        [Environment]::SetEnvironmentVariable($k, $vars[$k])
    }
}
function Restore-Env {
    foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k]) }
}

$fakeProc = $null
$apiProc = $null
try {
    # 3. PayOS gia lap + API (bien moi truong duoc tien trinh con ke thua)
    Set-TempEnv @{
        PAYOS_FAKE_PORT = "$PayOsPort"
        PAYOS_FAKE_WEBHOOK = "$api/api/payments/payos/webhook"
        ASPNETCORE_ENVIRONMENT = "Development"
        ConnectionStrings__DefaultConnection = $connection
        Jwt__Key = $jwtKey
        PayOs__Enabled = "true"
        PayOs__ApiBaseUrl = $fake
        PayOs__ClientId = "nursinghome-demo-client-id"
        PayOs__ApiKey = "nursinghome-demo-api-key"
        PayOs__ChecksumKey = "nursinghome-demo-checksum-key"
        RateLimit__CredentialsPerMinute = "500"
        Ai__Enabled = "true"
        Ai__Provider = "Fake"
        API_BASE = $api
        PAYOS_FAKE_BASE = $fake
    }
    $fakeProc = Start-Process node -ArgumentList "`"$(Join-Path $root 'scripts\payos-fake\server.mjs')`"" -PassThru -WindowStyle Hidden
    $apiProc = Start-Process dotnet -ArgumentList "NursingHome.API.dll --urls $api" -WorkingDirectory $out -PassThru -WindowStyle Hidden

    Write-Host ">> Cho API $api ..." -ForegroundColor Cyan
    $ready = $false
    for ($i = 0; $i -lt 60 -and -not $ready; $i++) {
        try { Invoke-WebRequest "$api/api/public/overview" -UseBasicParsing -TimeoutSec 2 | Out-Null; $ready = $true }
        catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ready) { throw "API khong khoi dong duoc o $api." }

    # 4. Smoke test
    node (Join-Path $root "scripts\smoke-test.mjs")
    $code = $LASTEXITCODE
}
finally {
    if ($apiProc -and -not $apiProc.HasExited) { Stop-Process -Id $apiProc.Id -Force }
    if ($fakeProc -and -not $fakeProc.HasExited) { Stop-Process -Id $fakeProc.Id -Force }
    Restore-Env
}
exit $code
