[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskServerExe = Join-Path $PSScriptRoot 'out/CloudSaveServer.exe'
if (-not (Test-Path -LiteralPath $taskServerExe)) { throw 'Publish the server before making a backup.' }
$taskBackup = Join-Path $PSScriptRoot ('data/backups/saves-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff') + '.db')
Push-Location -LiteralPath $PSScriptRoot
try {
    & $taskServerExe --backup $taskBackup
    if ($LASTEXITCODE -ne 0) { throw "Backup failed with code $LASTEXITCODE" }
} finally {
    Pop-Location
}
