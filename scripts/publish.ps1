param([ValidateSet("win-x64", "win-arm64")][string]$Runtime = "win-x64")
$ErrorActionPreference = "Stop"
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet build EpochTray.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    dotnet run --project tests/EpochTray.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
    dotnet publish src/EpochTray/EpochTray.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "artifacts/$Runtime"
    if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
    Write-Host "Ready: artifacts/$Runtime/EpochTray.exe"
} finally { Pop-Location }
