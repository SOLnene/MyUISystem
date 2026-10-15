[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskServerExe = Join-Path $PSScriptRoot 'out/CloudSaveServer.exe'
if (-not (Test-Path -LiteralPath $taskServerExe)) {
    throw 'The server is not published. Use .NET 10 SDK: dotnet publish CloudSaveServer.csproj -c Release -r win-x64 --self-contained true -o out'
}
Push-Location -LiteralPath $PSScriptRoot
try {
    Write-Host 'Cloud save server: http://127.0.0.1:5127/health'
    Write-Host 'Keep this terminal open. Press Ctrl+C to stop.'
    & $taskServerExe
    if ($LASTEXITCODE -ne 0) { throw "Server exited with code $LASTEXITCODE" }
} finally {
    Pop-Location
}
