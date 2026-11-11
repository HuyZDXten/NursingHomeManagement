param([switch]$Initialize)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$server = Read-Host 'SQL Server (Enter = localhost)'
if ([string]::IsNullOrWhiteSpace($server)) {
    $server = 'localhost'
}
$credential = Get-Credential -Message 'SQL Server login'
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection['Server'] = $server
$connection['Database'] = 'NursingHomeManagement'
$connection['User ID'] = $credential.UserName
$connection['Password'] = $credential.GetNetworkCredential().Password
$connection['Encrypt'] = 'True'
$connection['TrustServerCertificate'] = 'True'
$env:ConnectionStrings__DefaultConnection = $connection.ConnectionString
$env:ASPNETCORE_ENVIRONMENT = 'Development'
try {
    if ($Initialize) {
        $demoSecret = Read-Host 'Demo password (12+ characters, uppercase, lowercase, digit, symbol)' -AsSecureString
        $env:Seed__DemoPassword = [System.Net.NetworkCredential]::new('', $demoSecret).Password
        $env:Seed__DemoEnabled = 'true'
        dotnet run --project BE --no-launch-profile -- --initialize-db
        if ($LASTEXITCODE -ne 0) {
            throw 'Database initialization failed.'
        }
    }
    dotnet dev-certs https --check
    if ($LASTEXITCODE -ne 0) {
        throw 'Run dotnet dev-certs https --trust once, then run this script again.'
    }

    dotnet run --project BE --launch-profile https
} finally {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    Remove-Item Env:Seed__DemoPassword -ErrorAction SilentlyContinue
    Remove-Item Env:Seed__DemoEnabled -ErrorAction SilentlyContinue
}
