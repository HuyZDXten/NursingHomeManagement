$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$server = Read-Host 'SQL Server (Enter = localhost)'
if ([string]::IsNullOrWhiteSpace($server)) {
    $server = 'localhost'
}
$credential = Get-Credential -Message 'SQL Server login for demo and isolated test databases'
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = $server
$connection['Database'] = 'NursingHomeManagement'
$connection['User ID'] = $credential.UserName
$connection['Password'] = $credential.GetNetworkCredential().Password
$connection['Encrypt'] = 'True'
$connection['TrustServerCertificate'] = 'True'
$env:ConnectionStrings__DefaultConnection = $connection.ConnectionString
$demoPassword = Read-Host 'Existing demo account password' -AsSecureString
$env:Seed__DemoPassword = [System.Net.NetworkCredential]::new('', $demoPassword).Password
try {
    dotnet test Tests --logger 'trx;LogFileName=nursinghome.trx' --results-directory work/TestResults
    if ($LASTEXITCODE -ne 0) {
        throw 'Some tests failed. Inspect work/TestResults.'
    }
} finally {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    Remove-Item Env:Seed__DemoPassword -ErrorAction SilentlyContinue
}
