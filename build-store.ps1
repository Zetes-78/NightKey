<#
.SYNOPSIS
  Builds NightKey-<version>.msix for the Microsoft Store.

.DESCRIPTION
  Publishes the Store build of NightKey (no "Check for updates": the Store updates it), fills in the package manifest
  and packs it with makeappx from the Windows SDK. Upload the .msix in Partner Center; the Store signs it.

  The three identity values come from Partner Center > your app > Product management > Product identity.
  On GitHub they're repository variables (STORE_IDENTITY_NAME, STORE_PUBLISHER, STORE_PUBLISHER_NAME).

.EXAMPLE
  .\build-store.ps1 -IdentityName 'NightTechSolutions.NightKey' -Publisher 'CN=12345678-ABCD-...' -PublisherName 'Night Tech Solutions'
#>
param(
    [string]$IdentityName = $env:STORE_IDENTITY_NAME,
    [string]$Publisher = $env:STORE_PUBLISHER,
    [string]$PublisherName = $env:STORE_PUBLISHER_NAME
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $IdentityName) { $IdentityName = 'NightTechSolutions.NightKey'; Write-Warning 'No Store identity given: using a placeholder. This package is for testing, not for upload.' }
if (-not $Publisher) { $Publisher = 'CN=Night Tech Solutions' }
if (-not $PublisherName) { $PublisherName = 'Night Tech Solutions' }

$project = 'src\NightKey\NightKey.csproj'
$version = (Select-Xml -Path $project -XPath '//Version').Node.InnerText
$msixVersion = "$version.0"   # the Store needs four parts, ending in 0
Write-Host "NightKey $version for the Microsoft Store" -ForegroundColor Cyan

Write-Host 'Running core tests...'
dotnet run --project tests\NightKey.CoreTests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed. Nothing was built.' }

$layout = 'publish-store'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
dotnet publish $project -c Release -o $layout -p:StoreBuild=true
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

# Manifest and tile images
$manifest = (Get-Content store\AppxManifest.xml -Raw).
    Replace('$IDENTITY_NAME$', [Security.SecurityElement]::Escape($IdentityName)).
    Replace('$PUBLISHER$', [Security.SecurityElement]::Escape($Publisher)).
    Replace('$PUBLISHER_NAME$', [Security.SecurityElement]::Escape($PublisherName)).
    Replace('$VERSION$', $msixVersion)
Set-Content -Path "$layout\AppxManifest.xml" -Value $manifest -Encoding UTF8
Copy-Item store\Assets "$layout\Assets" -Recurse -Force

# makeappx from the newest Windows SDK
$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]($_.Directory.Parent.Name) } -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe not found. Install the Windows SDK (winget install Microsoft.WindowsSDK.10.0.26100).' }

New-Item -ItemType Directory -Force dist | Out-Null
$out = "dist\NightKey-$version.msix"
if (Test-Path $out) { Remove-Item $out }
& $makeappx.FullName pack /d $layout /p $out /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed.' }
$hash = (Get-FileHash $out -Algorithm SHA256).Hash.ToLower()
"$hash  $(Split-Path $out -Leaf)" | Out-File -Encoding ascii "$out.sha256.txt"
Write-Host "Done: $out" -ForegroundColor Green
