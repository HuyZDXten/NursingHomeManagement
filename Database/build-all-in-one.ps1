<#
.SYNOPSIS
  Gop 00_CreateDatabase.sql -> 05_DemoSeed.sql thanh MOT file: Database\NursingHomeDB_AllInOne.sql.

.DESCRIPTION
  File gop dung de tao NursingHomeDB ma khong can PowerShell: mo bang SSMS roi bam Execute (F5),
  hoac chay mot lenh sqlcmd. Ket qua giong setup-local-db.ps1: tao DB (Vietnamese_CI_AS,
  READ_COMMITTED_SNAPSHOT) roi chay 01..05 theo dung thu tu.

  00..05 van la nguon goc. Sua 00..05 xong thi chay lai script nay va commit ca file sinh ra.
  -Check chi kiem tra file gop con khop voi 00..05 khong (exit 1 neu da cu), dung truoc khi mo PR.

  Khi gop, giua cac file co them:
    - "GO" (vai file khong ket thuc bang GO, khong co thi batch cuoi dinh vao file sau);
    - lenh dat lai SET XACT_ABORT/NOCOUNT/ANSI_NULLS/QUOTED_IDENTIFIER, vi setup-local-db.ps1
      chay moi file bang mot ket noi sqlcmd moi (-I) nen moi file bat dau voi thiet lap mac dinh.

.EXAMPLE
  .\Database\build-all-in-one.ps1            # sinh lai NursingHomeDB_AllInOne.sql
  .\Database\build-all-in-one.ps1 -Check     # chi kiem tra, khong ghi file
  .\Database\build-all-in-one.ps1 -Database NursingHomeDB_Thu -OutFile "$env:TEMP\thu.sql"   # ban thu voi ten DB khac
#>
param(
    [string]$Database = "NursingHomeDB",
    [string]$OutFile,
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $OutFile) { $OutFile = Join-Path $root "NursingHomeDB_AllInOne.sql" }
if ($Database -notmatch '^[A-Za-z0-9_]+$') { throw "Ten database chi gom chu, so, dau gach duoi." }

$parts = @(
    "00_CreateDatabase.sql",
    "01_Schema.sql",
    "02_Views_Functions.sql",
    "03_StoredProcedures.sql",
    "04_ReferenceData.sql",
    "05_DemoSeed.sql"
)
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Read-Part([string]$name) {
    $text = [IO.File]::ReadAllText((Join-Path $root $name), $utf8)
    return $text.TrimStart([char]0xFEFF).Replace("`r`n", "`n").TrimEnd()
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add(@"
/* ============================================================
   NursingHomeDB_AllInOne.sql - FILE TU SINH, DUNG SUA TAY
   ------------------------------------------------------------
   Gop 00_CreateDatabase.sql -> 05_DemoSeed.sql thanh mot file de tao
   database $Database (schema + view + thu tuc + danh muc + du lieu demo)
   giong het setup-local-db.ps1.

   Sinh lai sau moi lan sua 00..05:  .\Database\build-all-in-one.ps1

   CACH CHAY (chon mot):
     SSMS:   File > Open > file nay, ket noi server, bam Execute (F5).
             Khong can bat SQLCMD Mode. Tab Messages phai ket thuc bang
             "NursingHomeDB_AllInOne.sql: xong." va khong co dong do.
     sqlcmd: sqlcmd -S localhost -E -C -b -I -x -f 65001 -i Database\NursingHomeDB_AllInOne.sql
             (-x: hash BCrypt co dau $, -f 65001: giu tieng Viet)

   Chay lai nhieu lan duoc: bang/du lieu da co thi bo qua, khong nhan ban.
   Muon xoa va tao lai tu dau: bo dau -- o 4 dong "TAO LAI TU DAU" ben duoi
   (MAT HET du lieu trong $Database, dung API truoc).

   Tai khoan demo: mat khau chung Demo@123456 (danh sach o phan 05_DemoSeed.sql).
   ============================================================ */
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE master;
GO

-- TAO LAI TU DAU (bo dau -- o 4 dong duoi):
-- IF DB_ID(N'$Database') IS NOT NULL
--     ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
-- IF DB_ID(N'$Database') IS NOT NULL DROP DATABASE [$Database];
-- GO
"@.Replace("`r`n", "`n"))

foreach ($part in $parts) {
    $text = Read-Part $part
    if ($part -eq "00_CreateDatabase.sql") {
        $text = [regex]::Replace($text, '\bNursingHomeDB\b', $Database)
    }

    $lines.Add("")
    $lines.Add("-- ============================================================")
    $lines.Add("-- $part")
    $lines.Add("-- ============================================================")
    if ($part -ne "00_CreateDatabase.sql") {
        $lines.Add("SET XACT_ABORT OFF; SET NOCOUNT OFF; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;")
        $lines.Add("GO")
    }
    $lines.Add($text)
    $lines.Add("GO")
    if ($part -eq "00_CreateDatabase.sql") {
        $lines.Add("")
        $lines.Add("USE [$Database];")
        $lines.Add("GO")
    }
}

$lines.Add("")
$lines.Add("PRINT N'NursingHomeDB_AllInOne.sql: xong.';")
$lines.Add("GO")

$content = ($lines -join "`n").Replace("`n", "`r`n") + "`r`n"

if ($Check) {
    if (-not (Test-Path $OutFile)) { Write-Host "Chua co $OutFile. Chay .\Database\build-all-in-one.ps1" -ForegroundColor Red; exit 1 }
    $current = [IO.File]::ReadAllText($OutFile, $utf8).TrimStart([char]0xFEFF).Replace("`r`n", "`n")
    if ($current -ne $content.Replace("`r`n", "`n")) {
        Write-Host "NursingHomeDB_AllInOne.sql da cu so voi 00..05. Chay .\Database\build-all-in-one.ps1 roi commit." -ForegroundColor Red
        exit 1
    }
    Write-Host "NursingHomeDB_AllInOne.sql khop voi 00..05." -ForegroundColor Green
    exit 0
}

# UTF-8 CO BOM de SSMS doc dung tieng Viet (khong BOM, SSMS co the doc thanh ma ANSI).
[IO.File]::WriteAllText($OutFile, $content, (New-Object System.Text.UTF8Encoding($true)))
Write-Host "Da ghi $OutFile ($([Math]::Round((Get-Item $OutFile).Length / 1KB)) KB)." -ForegroundColor Green
