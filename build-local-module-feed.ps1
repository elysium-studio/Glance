param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$catalog = Get-Content (Join-Path $PSScriptRoot "module-catalog.json") -Raw | ConvertFrom-Json
$packages = [Collections.Generic.List[object]]::new()
$directory = Split-Path $OutputPath
$iconRoot = Join-Path $directory "ModuleIcons"
[void](New-Item $directory -ItemType Directory -Force)

function Get-Sha256
{
    param([string]$Path)

    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()

    try
    {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace("-", "")
    }
    finally
    {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function Copy-ModuleIcon
{
    param([object]$Metadata, [object]$Visuals, [string]$PropertyName)

    if ($null -eq $Visuals)
    {
        return $null
    }

    $property = $Visuals.PSObject.Properties[$PropertyName]

    if ($null -eq $property -or [string]::IsNullOrWhiteSpace($property.Value))
    {
        return $null
    }

    $sourcePath = Join-Path $PSScriptRoot $property.Value

    if (-not (Test-Path $sourcePath -PathType Leaf))
    {
        throw "The module icon $sourcePath was not found."
    }

    $destinationDirectory = Join-Path $iconRoot "$($Metadata.id)\$($Metadata.version)"
    [void](New-Item $destinationDirectory -ItemType Directory -Force)
    $destinationPath = Join-Path $destinationDirectory (Split-Path $sourcePath -Leaf)
    Copy-Item $sourcePath $destinationPath -Force
    return ([Uri][IO.Path]::GetFullPath($destinationPath)).AbsoluteUri
}

foreach ($metadata in $catalog.modules)
{
    $packagePath = Join-Path $PackageDirectory "$($metadata.id).glance"

    if (-not (Test-Path $packagePath -PathType Leaf))
    {
        throw "The local module feed is missing $($metadata.id).glance."
    }

    $package = Get-Item $packagePath
    $visualProperty = $catalog.visuals.PSObject.Properties[$metadata.id]
    $visuals = if ($null -eq $visualProperty) { $null } else { $visualProperty.Value }
    $iconUri = Copy-ModuleIcon $metadata $visuals "iconPath"
    $lightIconUri = Copy-ModuleIcon $metadata $visuals "lightIconPath"
    $icon = [ordered]@{
        type = "glyph"
        source = $metadata.iconGlyph
        lightSource = $null
        fontFamily = "Segoe Fluent Icons"
        accentColor = if ($null -eq $visuals) { $null } else { $visuals.accentColor }
        lightAccentColor = if ($null -eq $visuals) { $null } else { $visuals.lightAccentColor }
    }

    if (-not [string]::IsNullOrWhiteSpace($iconUri))
    {
        $icon.type = "bitmap"
        $icon.source = $iconUri
        $icon.lightSource = $lightIconUri
        $icon.fontFamily = $null
    }
    elseif ($null -ne $visuals -and -not [string]::IsNullOrWhiteSpace($visuals.iconPathData))
    {
        $icon.type = "path"
        $icon.source = $visuals.iconPathData
        $icon.lightSource = $visuals.lightIconPathData
        $icon.fontFamily = $null
    }

    $packages.Add([ordered]@{
        id = $metadata.id
        version = $metadata.version
        moduleApiVersion = 1
        minimumGlanceVersion = "0.1.0"
        displayName = $metadata.displayName
        description = $metadata.description
        category = $metadata.category
        categoryDisplayName = $metadata.categoryDisplayName
        categoryGlyph = $metadata.categoryGlyph
        categoryOrder = $metadata.categoryOrder
        icon = $icon
        order = $metadata.order
        downloadUrl = ([Uri]$package.FullName).AbsoluteUri
        sha256 = Get-Sha256 $package.FullName
        size = $package.Length
        isDelisted = $false
        isRevoked = $false
        isVisible = $metadata.visible
        capabilities = @($metadata.capabilities)
        dependencies = @($metadata.dependencies)
    })
}

$feed = [ordered]@{
    schemaVersion = 1
    channel = "stable"
    displayName = "Local solution"
    generatedAt = [DateTimeOffset]::UtcNow.ToString("O")
    modules = $packages
}

$feed | ConvertTo-Json -Depth 10 | Set-Content $OutputPath -Encoding UTF8
