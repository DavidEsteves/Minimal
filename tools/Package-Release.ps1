[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content (Join-Path $projectRoot 'Minimal/Minimal.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a stable semantic version in Minimal.csproj.' }
$releaseRoot = Join-Path $projectRoot 'artifacts/release'
# Always publish into a fresh directory so stale local binaries cannot enter the ZIP.
$publishDirectory = Join-Path $releaseRoot ("staging-" + [Guid]::NewGuid().ToString('N'))
$buildDirectory = Join-Path $releaseRoot ("build-" + [Guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $releaseRoot "Minimal-$version-win-x64.zip"

dotnet publish (Join-Path $projectRoot 'Minimal/Minimal.csproj') -c Release `
    -p:PublishProfile=WinX64 -p:TreatWarningsAsErrors=true `
    -p:DebugType=None -p:DebugSymbols=false "-p:BaseOutputPath=$buildDirectory/" -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }

foreach ($required in @('Minimal.exe', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'Licenses')) {
    if (-not (Test-Path (Join-Path $publishDirectory $required))) { throw "Missing release content: $required" }
}
@"
Minimal $version — Windows x64

Extract the entire ZIP to a folder, then run Minimal.exe.
Requires Windows 10/11 x64 and the .NET 8 Desktop Runtime.
Markdown preview also requires the Microsoft Edge WebView2 Evergreen Runtime.
Keep LICENSE, THIRD-PARTY-NOTICES.md, and Licenses alongside the executable.

Documentation: https://github.com/DavidEsteves/Minimal#readme
"@ | Set-Content (Join-Path $publishDirectory 'README.txt') -Encoding utf8

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archivePath -Force
$hash = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $archivePath -Leaf)" | Set-Content (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Release package: $archivePath"
Write-Output "SHA256: $hash"
