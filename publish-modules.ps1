param(
    [string[]]$Module = @(),
    [switch]$All,
    [string]$ChangedSince = "",
    [string]$ConfigurationPath = "",
    [string]$SftpHost = "",
    [string]$SftpUser = "",
    [string]$SftpPassword = "",
    [string]$SftpBasePath = "",
    [string]$SftpHostKey = "",
    [switch]$SkipUpload
)

$ErrorActionPreference = "Stop"
$catalogPath = Join-Path $PSScriptRoot "module-catalog.json"
$catalog = Get-Content $catalogPath -Raw | ConvertFrom-Json

if ($catalog.schemaVersion -ne 1)
{
    throw "The module catalogue format is not supported."
}

if ([string]::IsNullOrWhiteSpace($ConfigurationPath))
{
    $ConfigurationPath = Join-Path $PSScriptRoot "publish.local.json"
}

$configuration = if (Test-Path $ConfigurationPath)
{
    Get-Content $ConfigurationPath -Raw | ConvertFrom-Json
}
else
{
    $null
}

function Resolve-PublishSetting
{
    param([string]$Value, [string]$EnvironmentName, $ConfiguredValue, [string]$DefaultValue = "")

    if (-not [string]::IsNullOrWhiteSpace($Value))
    {
        return $Value
    }

    $environmentValue = [Environment]::GetEnvironmentVariable($EnvironmentName)

    if (-not [string]::IsNullOrWhiteSpace($environmentValue))
    {
        return $environmentValue
    }

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredValue))
    {
        return [string]$ConfiguredValue
    }

    return $DefaultValue
}

function Get-WinScpPath
{
    foreach ($path in @("C:\Program Files (x86)\WinSCP\WinSCP.com", "C:\Program Files\WinSCP\WinSCP.com"))
    {
        if (Test-Path $path)
        {
            return $path
        }
    }

    return ""
}

function Format-WinScpValue
{
    param([string]$Value)

    return '"' + $Value.Replace('"', '""') + '"'
}

function Test-PackageContentEqual([string]$FirstPath, [string]$SecondPath)
{
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $firstArchive = [IO.Compression.ZipFile]::OpenRead($FirstPath)
    $secondArchive = [IO.Compression.ZipFile]::OpenRead($SecondPath)

    try
    {
        $firstEntries = @($firstArchive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | Sort-Object FullName)
        $secondEntries = @($secondArchive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | Sort-Object FullName)

        if ($firstEntries.Count -ne $secondEntries.Count)
        {
            return $false
        }

        for ($index = 0; $index -lt $firstEntries.Count; $index++)
        {
            $firstEntry = $firstEntries[$index]
            $secondEntry = $secondEntries[$index]

            if ($firstEntry.FullName -cne $secondEntry.FullName -or $firstEntry.Length -ne $secondEntry.Length)
            {
                return $false
            }

            $hash = [Security.Cryptography.SHA256]::Create()
            $firstStream = $firstEntry.Open()
            $secondStream = $secondEntry.Open()

            try
            {
                $firstHash = [Convert]::ToHexString($hash.ComputeHash($firstStream))
                $secondHash = [Convert]::ToHexString($hash.ComputeHash($secondStream))
            }
            finally
            {
                $firstStream.Dispose()
                $secondStream.Dispose()
                $hash.Dispose()
            }

            if ($firstHash -cne $secondHash)
            {
                return $false
            }
        }

        return $true
    }
    finally
    {
        $firstArchive.Dispose()
        $secondArchive.Dispose()
    }
}

function Get-SelectedModules
{
    if ($All)
    {
        return @($catalog.modules)
    }

    if ($Module.Count -gt 0)
    {
        $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $Module | ForEach-Object { [void]$ids.Add($_) }
        $selected = @($catalog.modules | Where-Object { $ids.Contains($_.id) })

        if ($selected.Count -ne $ids.Count)
        {
            throw "One or more requested modules are not in module-catalog.json."
        }

        return $selected
    }

    $base = $ChangedSince

    if ([string]::IsNullOrWhiteSpace($base))
    {
        $base = (git describe --tags --abbrev=0 2>$null)
    }

    if ([string]::IsNullOrWhiteSpace($base))
    {
        throw "Choose modules, use -All, or provide -ChangedSince."
    }

    $changedPaths = @(git diff --name-only $base HEAD)

    if ($LASTEXITCODE -ne 0)
    {
        throw "Unable to determine changed modules."
    }

    if ($changedPaths | Where-Object { $_ -like "Glance.Application.Abstractions/*" })
    {
        return @($catalog.modules)
    }

    return @($catalog.modules | Where-Object {
        $entry = $_
        $changedPaths | Where-Object {
            $changedPath = $_.Replace('\', '/')
            $entry.paths | Where-Object { $changedPath.StartsWith("$_/", [StringComparison]::OrdinalIgnoreCase) }
        }
    })
}

function Set-PackageManifest([string]$PackagePath, $Metadata)
{
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($PackagePath, [IO.Compression.ZipArchiveMode]::Update)

    try
    {
        $existingEntry = $archive.GetEntry("module.json")

        if ($null -ne $existingEntry)
        {
            $existingEntry.Delete()
        }
        $entry = $archive.CreateEntry("module.json", [IO.Compression.CompressionLevel]::Optimal)
        $stream = $entry.Open()
        $writer = [IO.StreamWriter]::new($stream)

        try
        {
            $manifest = [ordered]@{
                schemaVersion = 1
                id = $Metadata.id
                version = $Metadata.version
                moduleApiVersion = 1
                minimumGlanceVersion = "0.1.0"
                displayName = $Metadata.displayName
                description = $Metadata.description
                category = $Metadata.category
                iconGlyph = $Metadata.iconGlyph
                isVisible = $Metadata.visible
                capabilities = @($Metadata.capabilities)
                dependencies = @($Metadata.dependencies)
            }
            $writer.Write(($manifest | ConvertTo-Json -Depth 8 -Compress))
        }
        finally
        {
            $writer.Dispose()
            $stream.Dispose()
        }
    }
    finally
    {
        $archive.Dispose()
    }
}

$selectedModules = @(Get-SelectedModules)

if ($selectedModules.Count -eq 0)
{
    Write-Host "No module packages need publishing."
    exit 0
}

$feedUri = "https://elysiumstud.io/feeds/glance/modules/stable/index.json"
$existingFeed = try
{
    Invoke-RestMethod $feedUri
}
catch
{
    if (-not $All)
    {
        throw "The existing module feed is unavailable. A partial publish cannot safely replace it."
    }

    [pscustomobject]@{ schemaVersion = 1; channel = "stable"; generatedAt = [DateTimeOffset]::UtcNow; modules = @() }
}

$artifactRoot = Join-Path $PSScriptRoot "artifacts\module-feed"
$moduleBuildRoot = Join-Path $artifactRoot "build"
$packageRoot = Join-Path $artifactRoot "packages"
$iconRoot = Join-Path $artifactRoot "icons"

if (Test-Path $artifactRoot)
{
    Remove-Item $artifactRoot -Recurse -Force
}

[void](New-Item $packageRoot -ItemType Directory -Force)
$moduleOutput = Join-Path $moduleBuildRoot "bin\Glance.Shell.WinUI\release_win-x64\Modules"

foreach ($versionGroup in $selectedModules | Group-Object version)
{
    dotnet build (Join-Path $PSScriptRoot "Glance.Shell.WinUI\Glance.Shell.WinUI.csproj") --configuration Release --artifacts-path $moduleBuildRoot --property:Platform=x64 --property:GlanceModuleVersion=$($versionGroup.Name) --warnaserror

    if ($LASTEXITCODE -ne 0)
    {
        throw "The module build failed."
    }

    foreach ($metadata in $versionGroup.Group)
    {
        $visualProperty = $catalog.visuals.PSObject.Properties[$metadata.id]
        $visuals = if ($null -eq $visualProperty) { $null } else { $visualProperty.Value }
        $sourcePath = Join-Path $moduleOutput "$($metadata.id).glance"

        if (-not (Test-Path $sourcePath -PathType Leaf))
        {
            throw "The module build did not produce $($metadata.id).glance."
        }

        $relativePackagePath = "packages/$($metadata.id)/$($metadata.version)/$($metadata.id).glance"
        $publicPackageUri = "https://elysiumstud.io/feeds/glance/modules/stable/$relativePackagePath"

        $destinationPath = Join-Path $artifactRoot $relativePackagePath
        [void](New-Item (Split-Path $destinationPath) -ItemType Directory -Force)
        Copy-Item $sourcePath $destinationPath
        Set-PackageManifest $destinationPath $metadata
        $existingPackage = Invoke-WebRequest $publicPackageUri -Method Head -SkipHttpErrorCheck

        if ($existingPackage.StatusCode -notin @(404, 410))
        {
            $existingPackagePath = Join-Path $env:TEMP "glance-module-$($metadata.id)-$([Guid]::NewGuid().ToString('N')).glance"

            try
            {
                Invoke-WebRequest $publicPackageUri -OutFile $existingPackagePath

                if (-not (Test-PackageContentEqual $destinationPath $existingPackagePath))
                {
                    throw "$($metadata.id) $($metadata.version) already exists with different content. Module package versions are immutable."
                }

                Copy-Item $existingPackagePath $destinationPath -Force
            }
            finally
            {
                Remove-Item $existingPackagePath -Force -ErrorAction SilentlyContinue
            }
        }

        $hash = (Get-FileHash $destinationPath -Algorithm SHA256).Hash
        $size = (Get-Item $destinationPath).Length
        $iconUri = $null
        $lightIconUri = $null

        foreach ($icon in @(@{ Property = "iconPath"; Variable = "iconUri" }, @{ Property = "lightIconPath"; Variable = "lightIconUri" }))
        {
            $property = if ($null -eq $visuals) { $null } else { $visuals.PSObject.Properties[$icon.Property] }

            if ($null -eq $property -or [string]::IsNullOrWhiteSpace($property.Value))
            {
                continue
            }

            $sourceIconPath = Join-Path $PSScriptRoot $property.Value

            if (-not (Test-Path $sourceIconPath -PathType Leaf))
            {
                throw "The module icon $sourceIconPath was not found."
            }

            $relativeIconPath = "icons/$($metadata.id)/$($metadata.version)/$(Split-Path $sourceIconPath -Leaf)"
            $destinationIconPath = Join-Path $artifactRoot $relativeIconPath
            [void](New-Item (Split-Path $destinationIconPath) -ItemType Directory -Force)
            Copy-Item $sourceIconPath $destinationIconPath
            Set-Variable -Name $icon.Variable -Value "https://elysiumstud.io/feeds/glance/modules/stable/$relativeIconPath"
        }

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

        $feedItem = [ordered]@{
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
            downloadUrl = $publicPackageUri
            sha256 = $hash
            size = $size
            isDelisted = $false
            isRevoked = $false
            isVisible = $metadata.visible
            capabilities = @($metadata.capabilities)
            dependencies = @($metadata.dependencies)
        }
        $existingFeed.modules = @($existingFeed.modules | Where-Object { $_.id -ne $metadata.id }) + [pscustomobject]$feedItem
    }
}

$existingFeed.schemaVersion = 1
$existingFeed.channel = "stable"
$existingFeed | Add-Member -NotePropertyName displayName -NotePropertyValue "Elysium Studio" -Force
$existingFeed.generatedAt = [DateTimeOffset]::UtcNow
$existingFeed.modules = @($existingFeed.modules | Sort-Object categoryOrder, order, displayName)
$indexPath = Join-Path $artifactRoot "index.json"
$existingFeed | ConvertTo-Json -Depth 10 | Set-Content $indexPath -Encoding UTF8

if ($SkipUpload)
{
    Write-Host "Module feed created at $artifactRoot"
    exit 0
}

$SftpHost = Resolve-PublishSetting $SftpHost "GLANCE_SFTP_HOST" $configuration.sftp.host
$SftpUser = Resolve-PublishSetting $SftpUser "GLANCE_SFTP_USER" $configuration.sftp.user
$SftpPassword = Resolve-PublishSetting $SftpPassword "GLANCE_SFTP_PASSWORD" $configuration.sftp.password
$SftpBasePath = Resolve-PublishSetting $SftpBasePath "GLANCE_SFTP_BASE_PATH" $configuration.sftp.basePath "/public"
$SftpHostKey = Resolve-PublishSetting $SftpHostKey "GLANCE_SFTP_HOST_KEY" $configuration.sftp.hostKey

if ([string]::IsNullOrWhiteSpace($SftpHost) -or [string]::IsNullOrWhiteSpace($SftpUser) -or [string]::IsNullOrWhiteSpace($SftpPassword) -or [string]::IsNullOrWhiteSpace($SftpHostKey))
{
    throw "SFTP publishing is not configured."
}

$winScpPath = Get-WinScpPath

if ([string]::IsNullOrWhiteSpace($winScpPath))
{
    throw "WinSCP.com was not found."
}

$remoteRoot = $SftpBasePath.Replace('\', '/').TrimEnd('/') + "/feeds/glance/modules/stable"
$scriptPath = Join-Path $env:TEMP "glance-module-upload-$([Guid]::NewGuid().ToString('N')).txt"
$commands = [Collections.Generic.List[string]]::new()
$commands.Add("option batch abort")
$commands.Add("option confirm off")
$commands.Add("option transfer binary")
$commands.Add("open sftp://$SftpHost/ -username=$(Format-WinScpValue $SftpUser) -password=$(Format-WinScpValue $SftpPassword) -hostkey=$(Format-WinScpValue $SftpHostKey)")
$commands.Add("option batch continue")
$commands.Add("mkdir $(Format-WinScpValue $SftpBasePath)")
$commands.Add("mkdir $(Format-WinScpValue ($SftpBasePath.Replace('\\', '/').TrimEnd('/') + '/feeds'))")
$commands.Add("mkdir $(Format-WinScpValue ($SftpBasePath.Replace('\\', '/').TrimEnd('/') + '/feeds/glance'))")
$commands.Add("mkdir $(Format-WinScpValue ($SftpBasePath.Replace('\\', '/').TrimEnd('/') + '/feeds/glance/modules'))")
$commands.Add("mkdir $(Format-WinScpValue $remoteRoot)")
$commands.Add("option batch abort")

foreach ($metadata in $selectedModules)
{
    $localDirectory = Join-Path $packageRoot "$($metadata.id)\$($metadata.version)"
    $remoteDirectory = "$remoteRoot/packages/$($metadata.id)/$($metadata.version)"
    $commands.Add("option batch continue")
    $commands.Add("mkdir $(Format-WinScpValue "$remoteRoot/packages")")
    $commands.Add("mkdir $(Format-WinScpValue "$remoteRoot/packages/$($metadata.id)")")
    $commands.Add("mkdir $(Format-WinScpValue $remoteDirectory)")
    $commands.Add("option batch abort")
    $commands.Add("put $(Format-WinScpValue (Join-Path $localDirectory "$($metadata.id).glance")) $(Format-WinScpValue "$remoteDirectory/$($metadata.id).glance")")

    $localIconDirectory = Join-Path $iconRoot "$($metadata.id)\$($metadata.version)"

    if (Test-Path $localIconDirectory -PathType Container)
    {
        $remoteIconDirectory = "$remoteRoot/icons/$($metadata.id)/$($metadata.version)"
        $commands.Add("option batch continue")
        $commands.Add("mkdir $(Format-WinScpValue "$remoteRoot/icons")")
        $commands.Add("mkdir $(Format-WinScpValue "$remoteRoot/icons/$($metadata.id)")")
        $commands.Add("mkdir $(Format-WinScpValue $remoteIconDirectory)")
        $commands.Add("option batch abort")

        foreach ($iconPath in Get-ChildItem $localIconDirectory -File)
        {
            $commands.Add("put $(Format-WinScpValue $iconPath.FullName) $(Format-WinScpValue "$remoteIconDirectory/$($iconPath.Name)")")
        }
    }
}

$commands.Add("put $(Format-WinScpValue $indexPath) $(Format-WinScpValue "$remoteRoot/index.json")")
$commands.Add("exit")

try
{
    $commands | Set-Content $scriptPath -Encoding UTF8
    & $winScpPath "/script=$scriptPath"

    if ($LASTEXITCODE -ne 0)
    {
        throw "The module feed upload failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Remove-Item $scriptPath -Force -ErrorAction SilentlyContinue
}

Write-Host "Published $($selectedModules.Count) module package(s)."
