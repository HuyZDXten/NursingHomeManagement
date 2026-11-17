<#
.SYNOPSIS
  Khoi dong moi thu de demo: (tuy chon) dung lai DB, PayOS gia hoac that, API, FE.

.DESCRIPTION
  Moi thanh phan chay trong mot cua so rieng de nhin duoc log. Che do PayOS duoc dat RO RANG bang bien moi truong
  (uu tien cao hon user-secrets va appsettings), nen khong bi lan khoa that voi khoa demo:
    Fake  PayOS gia o cong 5199 (scripts/payos-fake), khoa demo. Khong co tien that. KHUYEN DUNG khi demo truoc hoi dong.
    Real  PayOS that (https://api-merchant.payos.vn), 3 khoa PayOs:ClientId/ApiKey/ChecksumKey lay tu user-secrets.

.EXAMPLE
  .\scripts\demo.ps1                  # PayOS gia, giu DB hien tai
  .\scripts\demo.ps1 -Recreate        # dung lai DB ve du lieu demo sach roi chay (nen lam truoc moi lan tap/demo)
  .\scripts\demo.ps1 -PayOs Real      # PayOS that
  .\scripts\demo.ps1 -Ai Fake         # tat Gemini, chatbot tra loi theo kich ban co san (du phong khi mat mang)
  .\scripts\demo.ps1 -Stop            # tat API, FE, PayOS gia
#>
param(
    [ValidateSet("Fake", "Real")][string]$PayOs = "Fake",
    [ValidateSet("Default", "Fake")][string]$Ai = "Default",
    [switch]$Recreate,
    [switch]$Stop,
    [switch]$Hidden,
    [string]$Server = "localhost"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$userSecretsId = "nursinghome-api-prn232"

function Stop-Port([int]$port) {
    Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}

Write-Host ">> Tat API (5080), FE (5173), PayOS gia (5199) neu dang chay" -ForegroundColor Cyan
5080, 5173, 5199 | ForEach-Object { Stop-Port $_ }
if ($Stop) { return }
Start-Sleep -Seconds 1

if ($Recreate) {
    & (Join-Path $root "Database\setup-local-db.ps1") -Server $Server -Recreate
}

# ---- Bien moi truong cho API ----
$vars = [ordered]@{
    "RateLimit__CredentialsPerMinute" = "300"   # demo dang nhap nhieu tai khoan lien tuc, khong bi chan 429
    "PayOs__Enabled"                  = "true"
}
if ($Ai -eq "Fake") { $vars["Ai__Provider"] = "Fake" }

if ($PayOs -eq "Fake") {
    $vars["PayOs__ApiBaseUrl"]  = "http://localhost:5199"
    $vars["PayOs__ClientId"]    = "nursinghome-demo-client-id"
    $vars["PayOs__ApiKey"]      = "nursinghome-demo-api-key"
    $vars["PayOs__ChecksumKey"] = "nursinghome-demo-checksum-key"
    $vars["PayOs__SyncIntervalSeconds"] = "20"
}
else {
    $keys = dotnet user-secrets list --id $userSecretsId | ForEach-Object { ($_ -split "=")[0].Trim() }
    $missing = @("PayOs:ClientId", "PayOs:ApiKey", "PayOs:ChecksumKey") | Where-Object { $keys -notcontains $_ }
    if ($missing) {
        throw "Thieu khoa PayOS that trong user-secrets: $($missing -join ', '). Dat bang: dotnet user-secrets set `"PayOs:ClientId`" `"...`" --id $userSecretsId"
    }
    $vars["PayOs__ApiBaseUrl"] = "https://api-merchant.payos.vn"   # chi dat dia chi, 3 khoa lay tu user-secrets
    $vars["PayOs__SyncIntervalSeconds"] = "10"
}

$envCommand = ($vars.GetEnumerator() | ForEach-Object { "`$env:$($_.Key)='$($_.Value)'" }) -join "; "
$style = if ($Hidden) { "Hidden" } else { "Normal" }

function Start-Window([string]$title, [string]$workDir, [string]$command) {
    $full = "`$host.UI.RawUI.WindowTitle='$title'; Set-Location -LiteralPath '$workDir'; $command"
    Start-Process powershell -WindowStyle $style -ArgumentList @("-NoExit", "-Command", $full) | Out-Null
}

if ($PayOs -eq "Fake") {
    Start-Window "NursingHome - PayOS gia" $root "node scripts/payos-fake/server.mjs"
}
Start-Window "NursingHome - API" (Join-Path $root "BE") "$envCommand; dotnet run --project NursingHome.API --launch-profile http"
Start-Window "NursingHome - FE" (Join-Path $root "FE") "npm run dev -- --port 5173 --strictPort"

Write-Host ">> Cho API http://localhost:5080 san sang ..." -ForegroundColor Cyan
$ready = $false
for ($i = 0; $i -lt 90 -and -not $ready; $i++) {
    Start-Sleep -Seconds 1
    try { $ready = (Invoke-WebRequest "http://localhost:5080/api/public/overview" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { }
}
if (-not $ready) { throw "API khong len sau 90 giay. Xem cua so 'NursingHome - API'." }

Write-Host ""
Write-Host "San sang. PayOS: $PayOs | AI: $Ai" -ForegroundColor Green
Write-Host "  Web:         http://localhost:5173"
Write-Host "  Swagger:     http://localhost:5080/swagger"
if ($PayOs -eq "Fake") { Write-Host "  PayOS gia:   http://localhost:5199/dev   (bam 'Khach da tra' de gui webhook)" }
Write-Host "  Tai khoan:   <vai-tro>@nursinghome.local / Demo@123456  (admin, receptionist, accountant, doctor, nurse, caregiver, family, family2)"
Write-Host "  Tat het:     .\scripts\demo.ps1 -Stop"
