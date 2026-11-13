<#
.SYNOPSIS
  Dung database NursingHomeDB tren may local tu cac script SQL trong thu muc Database/.

.DESCRIPTION
  Database-first: API KHONG tao/migrate DB. Script nay chay lan luot:
    00_CreateDatabase.sql      (tren master)
    01_Schema.sql              bang, khoa, CHECK, index
    02_Views_Functions.sql     view bao cao
    03_StoredProcedures.sql    thu tuc bao cao
    04_ReferenceData.sql       Admin dau tien + danh muc goi dich vu
    05_DemoSeed.sql            du lieu demo (bo qua bang -SkipDemoSeed)

  Moi thay doi schema/seed phai sua vao cac file tren (xem Database/README.md).
  Dung API truoc khi chay -Recreate: ket noi dang mo se chan DROP DATABASE.

.EXAMPLE
  .\Database\setup-local-db.ps1                      # tao moi neu chua co, giu du lieu cu
  .\Database\setup-local-db.ps1 -Recreate            # xoa DB va dung lai tu dau
  .\Database\setup-local-db.ps1 -Server ".\SQLEXPRESS" -SqlUser sa -SqlPassword "..."
#>
param(
    [string]$Server = "localhost",
    [string]$Database = "NursingHomeDB",
    [string]$SqlUser,
    [string]$SqlPassword,
    [switch]$Recreate,
    [switch]$SkipDemoSeed
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "Khong tim thay sqlcmd. Cai 'Microsoft Command Line Utilities for SQL Server' roi chay lai."
}

# -b dung khi loi, -I bat QUOTED_IDENTIFIER (filtered index), -f 65001 giu tieng Viet, -x tat bien $(...) cua sqlcmd (hash BCrypt co dau $).
$auth = if ($SqlUser) { @("-U", $SqlUser, "-P", $SqlPassword) } else { @("-E") }
$common = @("-S", $Server, "-C", "-b", "-I", "-x", "-f", "65001") + $auth

function Invoke-SqlFile([string]$file, [string]$db) {
    Write-Host ">> $file" -ForegroundColor Cyan
    & sqlcmd @common -d $db -i (Join-Path $root $file)
    if ($LASTEXITCODE -ne 0) { throw "Loi khi chay $file (exit $LASTEXITCODE)." }
}

if ($Recreate) {
    Write-Host ">> Xoa database $Database" -ForegroundColor Yellow
    $drop = "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END"
    & sqlcmd @common -d master -Q $drop
    if ($LASTEXITCODE -ne 0) { throw "Khong xoa duoc database $Database." }
}

# Tao database theo tham so -Database (00_CreateDatabase.sql giu cho ai chay sqlcmd tay voi ten mac dinh).
# Ten khac (vd. NursingHomeDB_Test) giup chay smoke test ma khong dung DB dang dev.
Write-Host ">> Tao database $Database (neu chua co)" -ForegroundColor Cyan
& sqlcmd @common -d master -Q "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database] COLLATE Vietnamese_CI_AS;"
if ($LASTEXITCODE -ne 0) { throw "Khong tao duoc database $Database." }
& sqlcmd @common -d master -Q "ALTER DATABASE [$Database] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;"
if ($LASTEXITCODE -ne 0) { throw "Khong bat duoc READ_COMMITTED_SNAPSHOT cho $Database." }

Invoke-SqlFile "01_Schema.sql" $Database
Invoke-SqlFile "02_Views_Functions.sql" $Database
Invoke-SqlFile "03_StoredProcedures.sql" $Database
Invoke-SqlFile "04_ReferenceData.sql" $Database
if (-not $SkipDemoSeed) {
    Invoke-SqlFile "05_DemoSeed.sql" $Database
}

Write-Host "Xong. Kiem tra: cd BE; dotnet run --project NursingHome.API -- --check-db" -ForegroundColor Green
