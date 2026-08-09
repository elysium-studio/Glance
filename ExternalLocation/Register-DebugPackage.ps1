param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"
$manifest = [xml](Get-Content -LiteralPath $ManifestPath -Raw)
$packageName = $manifest.Package.Identity.Name
$externalPath = Split-Path ([System.IO.Path]::GetFullPath($ExecutablePath)) -Parent
$externalLocation = $externalPath.TrimEnd('\') + '\'
$manifestDirectory = Split-Path ([System.IO.Path]::GetFullPath($ManifestPath)) -Parent
$sourceAssets = Join-Path $externalPath "Assets"
$packageAssets = Join-Path $manifestDirectory "Assets"

if (!(Test-Path -LiteralPath $sourceAssets))
{
    throw "The application assets directory was not found at '$sourceAssets'."
}

$null = New-Item -ItemType Directory -Path $packageAssets -Force
Copy-Item -Path (Join-Path $sourceAssets "*") -Destination $packageAssets -Recurse -Force

$assetRegistration = Get-ChildItem -LiteralPath $sourceAssets -File -Recurse |
    Sort-Object FullName |
    ForEach-Object { "$(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 | Select-Object -ExpandProperty Hash) $($_.FullName.Substring($sourceAssets.Length))" }
$registrationPath = "$ManifestPath.registration"
$registration = "$(Get-FileHash -LiteralPath $ManifestPath -Algorithm SHA256 | Select-Object -ExpandProperty Hash)`n$($assetRegistration -join "`n")`n$externalLocation"
$installedPackage = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue | Select-Object -First 1

if ($null -ne $installedPackage -and
    (Test-Path -LiteralPath $registrationPath) -and
    (Get-Content -LiteralPath $registrationPath -Raw) -eq $registration)
{
    exit 0
}

if ($null -ne $installedPackage)
{
    Remove-AppxPackage -Package $installedPackage.PackageFullName -PreserveApplicationData -ErrorAction Stop
}

Add-AppxPackage -Register ([System.IO.Path]::GetFullPath($ManifestPath)) -ExternalLocation $externalLocation -ForceApplicationShutdown -ErrorAction Stop
Set-Content -LiteralPath $registrationPath -Value $registration -NoNewline
