param(
    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = "Stop"
$externalPath = Split-Path ([System.IO.Path]::GetFullPath($ExecutablePath)) -Parent
$externalLocation = $externalPath.TrimEnd('\') + '\'
Add-AppxPackage -Register ([System.IO.Path]::GetFullPath($ManifestPath)) -ExternalLocation $externalLocation -ForceApplicationShutdown -ErrorAction Stop
