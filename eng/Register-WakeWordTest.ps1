[CmdletBinding()]
param(
    [switch]$NoBuild,
    [switch]$NoLaunch,
    [switch]$Unregister
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "Glance.Shell.WinUI\Glance.Shell.WinUI.csproj"
$outputPath = Join-Path $repositoryRoot "Glance.Shell.WinUI\bin\x64\Debug\net11.0-windows10.0.26100.0\win-x64"
$manifestPath = Join-Path $repositoryRoot "eng\WakeWordIdentity\AppxManifest.xml"
$packageName = "ElysiumStudio.Glance.WakeWordTest"

Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue | Remove-AppxPackage

if ($Unregister)
{
    Write-Host "Removed the Glance wake-word test identity."
    exit 0
}

if (!$NoBuild)
{
    dotnet build $projectPath --configuration Debug --property:Platform=x64

    if ($LASTEXITCODE -ne 0)
    {
        throw "The Glance test build failed."
    }
}

$executablePath = Join-Path $outputPath "Glance.exe"

if (!(Test-Path -LiteralPath $executablePath))
{
    throw "Glance.exe was not found at $executablePath."
}

Add-AppxPackage -Register $manifestPath -ExternalLocation $outputPath
Write-Host "Registered package identity for $outputPath."

if (!$NoLaunch)
{
    $package = Get-AppxPackage -Name $packageName | Select-Object -First 1

    if ($null -eq $package)
    {
        throw "The Glance wake-word test identity was not registered."
    }

    $appUserModelId = "$($package.PackageFamilyName)!WakeWordTest"
    Start-Process -FilePath "explorer.exe" -ArgumentList "shell:AppsFolder\$appUserModelId"
}
