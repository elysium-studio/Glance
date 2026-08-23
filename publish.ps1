param(
    [string]$Version = "",
    [string[]]$ReleaseNotes = @(),
    [string]$Bump = "",
    [string]$ConfigurationPath = "",
    [string]$AzureSigningEndpoint = "",
    [string]$AzureSigningAccountName = "",
    [string]$AzureSigningCertProfileName = "",
    [string]$SftpHost = "",
    [string]$SftpUser = "",
    [string]$SftpPassword = "",
    [string]$SftpBasePath = "",
    [string]$SftpHostKey = "",
    [string]$MicrosoftStoreProductId = "",
    [string]$MicrosoftStoreIdentityName = "",
    [string]$MicrosoftStorePublisher = "",
    [string]$MicrosoftStorePublisherDisplayName = "",
    [string]$MicrosoftStoreTenantId = "",
    [string]$MicrosoftStoreSellerId = "",
    [string]$MicrosoftStoreClientId = "",
    [string]$MicrosoftStoreClientSecret = "",
    [switch]$SkipSftpUpload,
    [switch]$SkipModuleUpload,
    [switch]$SkipSigning,
    [switch]$SkipGitRelease,
    [switch]$SkipMicrosoftStore,
    [switch]$MicrosoftStoreDraft,
    [switch]$MicrosoftStorePackageOnly,
    [string]$MicrosoftStoreFlightId = "",
    [switch]$GitReleaseOnly,
    [switch]$FinalizeReleaseOnly,
    [switch]$Local
)

$ErrorActionPreference = "Stop"

if (-not $Local -and $SkipSigning)
{
    throw "Package-with-external-location releases must be signed. Use -Local for an unsigned local validation build."
}

if ([string]::IsNullOrWhiteSpace($ConfigurationPath))
{
    $ConfigurationPath = Join-Path $PSScriptRoot "publish.local.json"
}

$localConfiguration = if (Test-Path $ConfigurationPath)
{
    Get-Content $ConfigurationPath -Raw | ConvertFrom-Json
}
else
{
    $null
}

function Resolve-PublishSetting
{
    param(
        [string]$Value,
        [string]$EnvironmentName,
        $LocalValue,
        [string]$DefaultValue = ""
    )

    if (-not [string]::IsNullOrWhiteSpace($Value))
    {
        return $Value
    }

    $environmentValue = [Environment]::GetEnvironmentVariable($EnvironmentName)

    if (-not [string]::IsNullOrWhiteSpace($environmentValue))
    {
        return $environmentValue
    }

    if (-not [string]::IsNullOrWhiteSpace($LocalValue))
    {
        return [string]$LocalValue
    }

    return $DefaultValue
}

$AzureSigningEndpoint = Resolve-PublishSetting $AzureSigningEndpoint "GLANCE_SIGNING_ENDPOINT" $localConfiguration.azureSigning.endpoint
$AzureSigningAccountName = Resolve-PublishSetting $AzureSigningAccountName "GLANCE_SIGNING_ACCOUNT_NAME" $localConfiguration.azureSigning.accountName
$AzureSigningCertProfileName = Resolve-PublishSetting $AzureSigningCertProfileName "GLANCE_SIGNING_CERTIFICATE_PROFILE_NAME" $localConfiguration.azureSigning.certificateProfileName
$SftpHost = Resolve-PublishSetting $SftpHost "GLANCE_SFTP_HOST" $localConfiguration.sftp.host
$SftpUser = Resolve-PublishSetting $SftpUser "GLANCE_SFTP_USER" $localConfiguration.sftp.user
$SftpPassword = Resolve-PublishSetting $SftpPassword "GLANCE_SFTP_PASSWORD" $localConfiguration.sftp.password
$SftpBasePath = Resolve-PublishSetting $SftpBasePath "GLANCE_SFTP_BASE_PATH" $localConfiguration.sftp.basePath "/public"
$SftpHostKey = Resolve-PublishSetting $SftpHostKey "GLANCE_SFTP_HOST_KEY" $localConfiguration.sftp.hostKey
$MicrosoftStoreProductId = Resolve-PublishSetting $MicrosoftStoreProductId "GLANCE_STORE_PRODUCT_ID" $localConfiguration.microsoftStore.productId
$MicrosoftStoreIdentityName = Resolve-PublishSetting $MicrosoftStoreIdentityName "GLANCE_STORE_IDENTITY_NAME" $localConfiguration.microsoftStore.identityName
$MicrosoftStorePublisher = Resolve-PublishSetting $MicrosoftStorePublisher "GLANCE_STORE_PUBLISHER" $localConfiguration.microsoftStore.publisher
$MicrosoftStorePublisherDisplayName = Resolve-PublishSetting $MicrosoftStorePublisherDisplayName "GLANCE_STORE_PUBLISHER_DISPLAY_NAME" $localConfiguration.microsoftStore.publisherDisplayName
$MicrosoftStoreTenantId = Resolve-PublishSetting $MicrosoftStoreTenantId "GLANCE_STORE_TENANT_ID" $localConfiguration.microsoftStore.tenantId
$MicrosoftStoreSellerId = Resolve-PublishSetting $MicrosoftStoreSellerId "GLANCE_STORE_SELLER_ID" $localConfiguration.microsoftStore.sellerId
$MicrosoftStoreClientId = Resolve-PublishSetting $MicrosoftStoreClientId "GLANCE_STORE_CLIENT_ID" $localConfiguration.microsoftStore.clientId
$MicrosoftStoreClientSecret = Resolve-PublishSetting $MicrosoftStoreClientSecret "GLANCE_STORE_CLIENT_SECRET" $localConfiguration.microsoftStore.clientSecret
$MicrosoftStoreFlightId = Resolve-PublishSetting $MicrosoftStoreFlightId "GLANCE_STORE_FLIGHT_ID" $localConfiguration.microsoftStore.flightId

$GitRemoteName = "origin"
$GitTagPrefix = "v"
$VelopackPackId = "Glance"
$VelopackInstallerName = "$VelopackPackId-win-Setup.exe"
$PublicInstallerName = "Glance-win-Setup.exe"

function Get-WinScpPath
{
    $winScpPaths = @(
        "C:\Program Files (x86)\WinSCP\WinSCP.com",
        "C:\Program Files\WinSCP\WinSCP.com"
    )

    foreach ($winScpPath in $winScpPaths)
    {
        if (Test-Path $winScpPath)
        {
            return $winScpPath
        }
    }

    return ""
}

function Format-WinScpValue
{
    param(
        [string]$Value
    )

    return '"' + $Value.Replace('"', '""') + '"'
}

function Send-SftpRelease
{
    param(
        [string]$FeedPath,
        [string]$ReleaseLogPath
    )

    if ([string]::IsNullOrWhiteSpace($SftpHost))
    {
        Write-Host "SFTP host has not been configured." -ForegroundColor Red
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace($SftpUser))
    {
        Write-Host "SFTP user has not been configured." -ForegroundColor Red
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace($SftpPassword))
    {
        Write-Host "SFTP password has not been configured." -ForegroundColor Red
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace($SftpHostKey))
    {
        Write-Host "SFTP host key has not been configured." -ForegroundColor Red
        exit 1
    }

    $winScpPath = Get-WinScpPath

    if ([string]::IsNullOrWhiteSpace($winScpPath))
    {
        Write-Host "WinSCP.com was not found. Install WinSCP first." -ForegroundColor Red
        exit 1
    }

    if ([string]::IsNullOrWhiteSpace($SftpBasePath))
    {
        $SftpBasePath = "/public"
    }

    $SftpBasePath = $SftpBasePath.Replace('\', '/').TrimEnd('/')
    $sftpFeedsPath = "$SftpBasePath/feeds"
    $sftpFeedPath = "$sftpFeedsPath/glance"
    $sftpReleaseLogPath = "$sftpFeedPath/releases.json"

    Write-Host ""
    Write-Host "Uploading release to SFTP..." -ForegroundColor Cyan
    Write-Host "Feed folder: $FeedPath -> $sftpFeedPath" -ForegroundColor DarkGray
    Write-Host "Release log: $ReleaseLogPath -> $sftpReleaseLogPath" -ForegroundColor DarkGray

    $winScpScriptPath = Join-Path $env:TEMP "glance-upload-$(Get-Date -Format 'yyyyMMddHHmmss').txt"
    $openCommand = "open sftp://$SftpHost/ -username=$(Format-WinScpValue $SftpUser) -password=$(Format-WinScpValue $SftpPassword) -hostkey=$(Format-WinScpValue $SftpHostKey)"

    try
    {
        @(
            "option batch abort"
            "option confirm off"
            "option transfer binary"
            $openCommand
            "option batch continue"
            "mkdir $(Format-WinScpValue $SftpBasePath)"
            "mkdir $(Format-WinScpValue $sftpFeedsPath)"
            "mkdir $(Format-WinScpValue $sftpFeedPath)"
            "option batch abort"
            "synchronize remote $(Format-WinScpValue $FeedPath) $(Format-WinScpValue $sftpFeedPath)"
            "put $(Format-WinScpValue $ReleaseLogPath) $(Format-WinScpValue $sftpReleaseLogPath)"
            "exit"
        ) | Set-Content $winScpScriptPath -Encoding UTF8

        & $winScpPath "/script=$winScpScriptPath"

        $winScpExitCode = $LASTEXITCODE

        if ($winScpExitCode -ne 0)
        {
            Write-Host "SFTP upload failed with exit code $winScpExitCode" -ForegroundColor Red
            exit $winScpExitCode
        }
    }
    finally
    {
        if (Test-Path $winScpScriptPath)
        {
            Remove-Item $winScpScriptPath -Force
        }
    }

    Write-Host "SFTP upload completed" -ForegroundColor Green
}

function Test-GitHubCliAuth
{
    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue

    if (-not $ghCommand)
    {
        Write-Host "GitHub CLI (gh) was not found on PATH. Install it from https://cli.github.com" -ForegroundColor Red
        exit 1
    }

    gh auth status 2>$null | Out-Null

    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Not logged in to GitHub CLI. Running 'gh auth login'..." -ForegroundColor Yellow
        gh auth login

        if ($LASTEXITCODE -ne 0)
        {
            Write-Host "GitHub login failed. Cannot create the release." -ForegroundColor Red
            exit 1
        }
    }
}

function Test-GitWorkingTree
{
    param(
        [string]$RepoPath
    )

    git -C $RepoPath rev-parse --is-inside-work-tree 2>$null | Out-Null

    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "$RepoPath is not inside a git repository." -ForegroundColor Red
        exit 1
    }

    $gitStatus = git -C $RepoPath status --porcelain

    if ($gitStatus)
    {
        Write-Host "Warning: working tree has uncommitted changes. The tag will still be created against the current HEAD." -ForegroundColor Yellow
    }
}

function Send-GitHubRelease
{
    param(
        [string]$RepoPath,
        [string]$Version,
        [string[]]$ReleaseNotes,
        [string[]]$AssetPaths
    )

    foreach ($assetPath in $AssetPaths)
    {
        if (-not (Test-Path $assetPath))
        {
            Write-Host "Release asset not found: $assetPath" -ForegroundColor Red
            exit 1
        }
    }

    Write-Host ""
    Write-Host "Creating GitHub release..." -ForegroundColor Cyan

    Test-GitHubCliAuth
    Test-GitWorkingTree -RepoPath $RepoPath

    $tagName = "$GitTagPrefix$Version"
    $releaseTitle = "Glance v$Version"
    $isPrerelease = $Version -match '-'

    $tagMessage = if ($ReleaseNotes.Count -gt 0) { $ReleaseNotes -join "`n" } else { $releaseTitle }

    git -C $RepoPath tag -a $tagName -m $tagMessage

    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Failed to create git tag $tagName" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    git -C $RepoPath push $GitRemoteName $tagName

    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Failed to push tag $tagName to $GitRemoteName" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Tag $tagName pushed to $GitRemoteName" -ForegroundColor DarkGray

    $notesBody = if ($ReleaseNotes.Count -gt 0) { ($ReleaseNotes | ForEach-Object { "- $_" }) -join "`n" } else { "No release notes provided." }
    $notesPath = Join-Path $env:TEMP "glance-release-notes-$(Get-Date -Format 'yyyyMMddHHmmss').md"

    try
    {
        Set-Content -Path $notesPath -Value $notesBody -Encoding UTF8

        $ghArgs = @(
            "release", "create", $tagName
            $AssetPaths
            "--title", $releaseTitle
            "--notes-file", $notesPath
        )

        if ($isPrerelease)
        {
            $ghArgs += "--prerelease"
        }

        Push-Location $RepoPath

        try
        {
            & gh @ghArgs
        }
        finally
        {
            Pop-Location
        }

        if ($LASTEXITCODE -ne 0)
        {
            Write-Host "GitHub release creation failed with exit code $LASTEXITCODE" -ForegroundColor Red
            exit $LASTEXITCODE
        }
    }
    finally
    {
        if (Test-Path $notesPath)
        {
            Remove-Item $notesPath -Force
        }
    }

    Write-Host "GitHub release $tagName created with $($AssetPaths.Count) assets" -ForegroundColor Green
}

function Assert-ModuleFreeRelease
{
    param(
        [string]$OutputDirectory,
        [string[]]$PackagePaths = @()
    )

    $bundledModuleFiles = @(Get-ChildItem $OutputDirectory -Filter "*.glance" -File -Recurse -ErrorAction SilentlyContinue)

    if ($bundledModuleFiles.Count -gt 0)
    {
        throw "The application release contains bundled module packages: $($bundledModuleFiles.FullName -join ', ')"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    foreach ($packagePath in $PackagePaths)
    {
        $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)

        try
        {
            $bundledEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.glance', [StringComparison]::OrdinalIgnoreCase) })

            if ($bundledEntries.Count -gt 0)
            {
                throw "$(Split-Path $packagePath -Leaf) contains bundled module packages: $($bundledEntries.FullName -join ', ')"
            }
        }
        finally
        {
            $archive.Dispose()
        }
    }
}

function Test-AzureSigningAuth
{
    $azCommand = Get-Command az -ErrorAction SilentlyContinue

    if (-not $azCommand)
    {
        Write-Host "Azure CLI (az) was not found on PATH. Install it from https://aka.ms/installazurecliwindows" -ForegroundColor Red
        exit 1
    }

    az account show 2>$null | Out-Null

    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Not logged in to Azure CLI. Running 'az login'..." -ForegroundColor Yellow
        az login

        if ($LASTEXITCODE -ne 0)
        {
            Write-Host "Azure login failed. Cannot sign the build." -ForegroundColor Red
            exit 1
        }
    }
}

function New-AzureSigningMetadataFile
{
    param(
        [string]$Path
    )

    $metadata = [ordered]@{
        Endpoint               = $AzureSigningEndpoint
        CodeSigningAccountName = $AzureSigningAccountName
        CertificateProfileName = $AzureSigningCertProfileName
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $json = ConvertTo-Json -InputObject $metadata -Depth 3
    [System.IO.File]::WriteAllText($Path, $json, $utf8NoBom)
}

function Assert-PublishValue
{
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value))
    {
        throw "$Name has not been configured"
    }
}

function Get-MakeAppxPath
{
    $windowsKitsPath = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $path = Get-ChildItem $windowsKitsPath -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\makeappx.exe" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $path)
    {
        throw "MakeAppx.exe was not found in the Windows SDK"
    }

    return $path
}

function Get-MakePriPath
{
    $windowsKitsPath = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $path = Get-ChildItem $windowsKitsPath -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\makepri.exe" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $path)
    {
        throw "MakePri.exe was not found in the Windows SDK"
    }

    return $path
}

function Get-SignToolPath
{
    $windowsKitsPath = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $path = Get-ChildItem $windowsKitsPath -Directory |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1

    if (-not $path)
    {
        throw "SignTool.exe was not found in the Windows SDK"
    }

    return $path
}

function Get-AzureCodeSigningDlibPath
{
    $toolStorePath = Join-Path $env:USERPROFILE ".dotnet\tools\.store\vpk"
    $path = Get-ChildItem $toolStorePath -Filter "Azure.CodeSigning.Dlib.dll" -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -ExpandProperty FullName -First 1

    if (-not $path)
    {
        throw "Azure.CodeSigning.Dlib.dll was not found in the Velopack tool installation"
    }

    return $path
}

function New-ExternalIdentityPackage
{
    param(
        [string]$Version,
        [string]$OutputDirectory,
        [string]$MetadataPath,
        [switch]$Sign
    )

    $numericVersion = $Version -replace '-.*$', ''

    if ($numericVersion -notmatch '^\d+\.\d+\.\d+$')
    {
        throw "External identity version must use major.minor.patch format"
    }

    $packageVersion = "$numericVersion.0"
    $identityRoot = Join-Path (Split-Path $OutputDirectory -Parent) "ExternalIdentity"
    $stagingPath = Join-Path $identityRoot "Staging"
    $packagePath = Join-Path $OutputDirectory "Glance.Identity.msix"
    $manifestTemplatePath = Join-Path $PSScriptRoot "ExternalLocation\Package.appxmanifest.template"

    if (Test-Path $identityRoot)
    {
        Remove-Item $identityRoot -Recurse -Force
    }

    New-Item $stagingPath -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $PSScriptRoot "Glance.Shell.WinUI\Assets") $stagingPath -Recurse -Force
    $manifest = (Get-Content $manifestTemplatePath -Raw).Replace("__VERSION__", $packageVersion)
    [System.IO.File]::WriteAllText((Join-Path $stagingPath "AppxManifest.xml"), $manifest, [System.Text.UTF8Encoding]::new($false))

    $makePriPath = Get-MakePriPath
    $priConfigPath = Join-Path $identityRoot "priconfig.xml"
    $resourcesPath = Join-Path $stagingPath "resources.pri"
    & $makePriPath createconfig /cf $priConfigPath /dq en-US /o

    if ($LASTEXITCODE -ne 0)
    {
        throw "External identity resource configuration failed with exit code $LASTEXITCODE"
    }

    & $makePriPath new /pr $stagingPath /cf $priConfigPath /of $resourcesPath /o

    if ($LASTEXITCODE -ne 0)
    {
        throw "External identity resource indexing failed with exit code $LASTEXITCODE"
    }

    $makeAppxPath = Get-MakeAppxPath
    & $makeAppxPath pack /o /d $stagingPath /nv /p $packagePath

    if ($LASTEXITCODE -ne 0)
    {
        throw "External identity package creation failed with exit code $LASTEXITCODE"
    }

    if ($Sign)
    {
        $signToolPath = Get-SignToolPath
        $dlibPath = Get-AzureCodeSigningDlibPath
        & $signToolPath sign /v /fd SHA256 /tr "http://timestamp.acs.microsoft.com" /td SHA256 /dlib $dlibPath /dmdf $MetadataPath $packagePath

        if ($LASTEXITCODE -ne 0)
        {
            throw "External identity package signing failed with exit code $LASTEXITCODE"
        }

        & $signToolPath verify /pa /v $packagePath

        if ($LASTEXITCODE -ne 0)
        {
            throw "External identity package signature verification failed with exit code $LASTEXITCODE"
        }
    }

    Write-Host "External identity package created: $packagePath" -ForegroundColor Green
}

function Convert-ToXmlValue
{
    param(
        [string]$Value
    )

    return [System.Security.SecurityElement]::Escape($Value)
}

function New-StoreImage
{
    param(
        [string]$Source,
        [string]$Destination,
        [int]$Width,
        [int]$Height
    )

    Add-Type -AssemblyName System.Drawing
    $sourceImage = [System.Drawing.Image]::FromFile($Source)

    try
    {
        $destinationImage = New-Object System.Drawing.Bitmap($Width, $Height)

        try
        {
            $graphics = [System.Drawing.Graphics]::FromImage($destinationImage)

            try
            {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

                $scale = [Math]::Min($Width / $sourceImage.Width, $Height / $sourceImage.Height)
                $drawWidth = [int][Math]::Round($sourceImage.Width * $scale)
                $drawHeight = [int][Math]::Round($sourceImage.Height * $scale)
                $drawX = [int](($Width - $drawWidth) / 2)
                $drawY = [int](($Height - $drawHeight) / 2)
                $graphics.DrawImage($sourceImage, $drawX, $drawY, $drawWidth, $drawHeight)
            }
            finally
            {
                $graphics.Dispose()
            }

            $destinationImage.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally
        {
            $destinationImage.Dispose()
        }
    }
    finally
    {
        $sourceImage.Dispose()
    }
}

function Send-MicrosoftStoreRelease
{
    param(
        [string]$Version,
        [string]$InputDirectory,
        [string]$ProductId,
        [string]$IdentityName,
        [string]$Publisher,
        [string]$PublisherDisplayName,
        [string]$TenantId,
        [string]$SellerId,
        [string]$ClientId,
        [string]$ClientSecret,
        [string]$FlightId,
        [switch]$NoCommit,
        [switch]$PackageOnly
    )

    Assert-PublishValue "Microsoft Store identity name" $IdentityName
    Assert-PublishValue "Microsoft Store publisher" $Publisher
    Assert-PublishValue "Microsoft Store publisher display name" $PublisherDisplayName

    if (-not $PackageOnly)
    {
        Assert-PublishValue "Microsoft Store product ID" $ProductId
    }

    if (-not (Test-Path $InputDirectory))
    {
        throw "Published application directory was not found: $InputDirectory"
    }

    if (-not (Test-Path (Join-Path $InputDirectory "Glance.exe")))
    {
        throw "Glance.exe was not found in $InputDirectory"
    }

    $numericVersion = $Version -replace '-.*$', ''

    if ($numericVersion -notmatch '^\d+\.\d+\.\d+$')
    {
        throw "Version must use major.minor.patch format with an optional prerelease suffix"
    }

    $packageVersion = "$numericVersion.0"
    $outputDirectory = Join-Path $PSScriptRoot "Publish\$Version\Store"
    $stagingPath = Join-Path $outputDirectory "Staging"
    $packagePath = Join-Path $outputDirectory "Glance-$Version.msix"
    $symbolsPath = Join-Path $outputDirectory "Symbols"
    $appxSymbolsPath = Join-Path $outputDirectory "Glance-$Version.appxsym"
    $uploadStagingPath = Join-Path $outputDirectory "Upload"
    $uploadPath = Join-Path $outputDirectory "Glance-$Version.msixupload"
    $manifestTemplatePath = Join-Path $PSScriptRoot "Store\Package.appxmanifest.template"
    $logoPath = Join-Path $PSScriptRoot "Glance.Shell.WinUI\Assets\Glance.png"

    New-Item $outputDirectory -ItemType Directory -Force | Out-Null

    foreach ($path in @($stagingPath, $symbolsPath, $uploadStagingPath))
    {
        if (Test-Path $path)
        {
            Remove-Item $path -Recurse -Force
        }
    }

    foreach ($path in @($packagePath, $appxSymbolsPath, $uploadPath))
    {
        if (Test-Path $path)
        {
            Remove-Item $path -Force
        }
    }

    New-Item $stagingPath -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $InputDirectory "*") $stagingPath -Recurse -Force

    $symbolFiles = @(Get-ChildItem $stagingPath -Filter "*.pdb" -File -Recurse)

    if ($symbolFiles.Count -gt 0)
    {
        New-Item $symbolsPath -ItemType Directory -Force | Out-Null

        foreach ($symbolFile in $symbolFiles)
        {
            Copy-Item $symbolFile.FullName (Join-Path $symbolsPath $symbolFile.Name) -Force
            Remove-Item $symbolFile.FullName -Force
        }
    }

    $storeAssetsPath = Join-Path $stagingPath "StoreAssets"
    New-Item $storeAssetsPath -ItemType Directory -Force | Out-Null
    New-StoreImage $logoPath (Join-Path $storeAssetsPath "StoreLogo.png") 50 50
    New-StoreImage $logoPath (Join-Path $storeAssetsPath "Square44x44Logo.png") 44 44
    New-StoreImage $logoPath (Join-Path $storeAssetsPath "Square150x150Logo.png") 150 150

    foreach ($targetSize in @(16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 256))
    {
        New-StoreImage $logoPath (Join-Path $storeAssetsPath "Square44x44Logo.targetsize-$($targetSize)_altform-unplated.png") $targetSize $targetSize
    }

    $manifest = Get-Content $manifestTemplatePath -Raw
    $manifest = $manifest.Replace("__IDENTITY_NAME__", (Convert-ToXmlValue $IdentityName))
    $manifest = $manifest.Replace("__PUBLISHER__", (Convert-ToXmlValue $Publisher))
    $manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", (Convert-ToXmlValue $PublisherDisplayName))
    $manifest = $manifest.Replace("__VERSION__", $packageVersion)
    [System.IO.File]::WriteAllText((Join-Path $stagingPath "AppxManifest.xml"), $manifest,
        [System.Text.UTF8Encoding]::new($false))

    $makeAppxPath = Get-MakeAppxPath
    & $makeAppxPath pack /d $stagingPath /p $packagePath /o

    if ($LASTEXITCODE -ne 0)
    {
        throw "Microsoft Store package creation failed with exit code $LASTEXITCODE"
    }

    if ($symbolFiles.Count -gt 0)
    {
        $symbolsArchivePath = "$appxSymbolsPath.zip"
        Compress-Archive -Path (Join-Path $symbolsPath "*") -DestinationPath $symbolsArchivePath -CompressionLevel Optimal -Force
        Move-Item $symbolsArchivePath $appxSymbolsPath -Force
    }

    New-Item $uploadStagingPath -ItemType Directory -Force | Out-Null
    Copy-Item $packagePath $uploadStagingPath

    if (Test-Path $appxSymbolsPath)
    {
        Copy-Item $appxSymbolsPath $uploadStagingPath
    }

    $uploadArchivePath = "$uploadPath.zip"
    Compress-Archive -Path (Join-Path $uploadStagingPath "*") -DestinationPath $uploadArchivePath -CompressionLevel NoCompression -Force
    Move-Item $uploadArchivePath $uploadPath -Force

    Write-Host "Microsoft Store package created: $uploadPath" -ForegroundColor Green

    if ($PackageOnly)
    {
        return
    }

    $msstore = Get-Command msstore -ErrorAction SilentlyContinue

    if (-not $msstore)
    {
        throw "Microsoft Store Developer CLI was not found"
    }

    $credentialValues = @($TenantId, $SellerId, $ClientId, $ClientSecret)
    $configuredCredentialCount = @($credentialValues | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count

    if ($configuredCredentialCount -gt 0 -and $configuredCredentialCount -lt $credentialValues.Count)
    {
        throw "Microsoft Store credentials must either all be configured or all be omitted"
    }

    if ($configuredCredentialCount -eq $credentialValues.Count)
    {
        & $msstore.Source reconfigure --tenantId $TenantId --sellerId $SellerId --clientId $ClientId --clientSecret $ClientSecret

        if ($LASTEXITCODE -ne 0)
        {
            throw "Microsoft Store Developer CLI authentication failed with exit code $LASTEXITCODE"
        }
    }

    $publishArguments = @(
        "publish"
        $PSScriptRoot
        "--inputFile", $uploadPath
        "--appId", $ProductId
    )

    if ($NoCommit)
    {
        $publishArguments += "--noCommit"
    }

    if (-not [string]::IsNullOrWhiteSpace($FlightId))
    {
        $publishArguments += @("--flightId", $FlightId)
    }

    & $msstore.Source @publishArguments

    if ($LASTEXITCODE -ne 0)
    {
        throw "Microsoft Store submission failed with exit code $LASTEXITCODE"
    }

    Write-Host "Microsoft Store submission completed for $ProductId" -ForegroundColor Green
}

$ProjectPath = "$PSScriptRoot\Glance.Shell.WinUI\Glance.Shell.WinUI.csproj"
$ReleaseLogPath = "$PSScriptRoot\Publish\releases.json"
$FeedPath = "$PSScriptRoot\Publish\Feed"
$SigningMetadataPath = "$PSScriptRoot\Publish\signing-metadata.json"

$releases = @()

if (Test-Path $ReleaseLogPath)
{
    $parsed = Get-Content $ReleaseLogPath -Raw | ConvertFrom-Json
    $releases = @() + $parsed
}

if ($GitReleaseOnly)
{
    if ($Version -eq "")
    {
        if ($releases.Count -eq 0)
        {
            Write-Host "No releases found in releases.json. Nothing to re-release." -ForegroundColor Red
            exit 1
        }

        $Version = $releases[-1].version
    }

    $existingRelease = $releases | Where-Object { $_.version -eq $Version } | Select-Object -Last 1

    if (-not $existingRelease)
    {
        Write-Host "Version $Version was not found in releases.json. Build it first without -GitReleaseOnly." -ForegroundColor Red
        exit 1
    }

    $InstallerPath = "$FeedPath\$PublicInstallerName"
    $FullPackagePath = "$FeedPath\$VelopackPackId-$Version-full.nupkg"
    $DeltaPackagePath = "$FeedPath\$VelopackPackId-$Version-delta.nupkg"
    $releaseAssetPaths = @($InstallerPath, $FullPackagePath, $DeltaPackagePath)

    foreach ($releaseAssetPath in $releaseAssetPaths)
    {
        if (-not (Test-Path $releaseAssetPath))
        {
            Write-Host "Release asset not found at $releaseAssetPath. Build it first without -GitReleaseOnly." -ForegroundColor Red
            exit 1
        }
    }

    $releaseNotes = @($existingRelease.releaseNotes)

    Write-Host ""
    Write-Host "Skipping build, signing, packaging and SFTP - releasing existing v$Version to GitHub only" -ForegroundColor Cyan

    Send-GitHubRelease -RepoPath $PSScriptRoot -Version $Version -ReleaseNotes $releaseNotes -AssetPaths $releaseAssetPaths

    exit 0
}

if ($FinalizeReleaseOnly)
{
    if ($Version -eq "")
    {
        throw "A version is required when finalizing an existing package."
    }

    $InstallerPath = "$FeedPath\$PublicInstallerName"
    $FullPackagePath = "$FeedPath\$VelopackPackId-$Version-full.nupkg"
    $DeltaPackagePath = "$FeedPath\$VelopackPackId-$Version-delta.nupkg"
    $OutputPath = "$PSScriptRoot\Publish\$Version\Assets"

    foreach ($releaseAssetPath in @($InstallerPath, $FullPackagePath, $DeltaPackagePath))
    {
        if (-not (Test-Path $releaseAssetPath))
        {
            throw "Release asset not found: $releaseAssetPath"
        }
    }

    Assert-ModuleFreeRelease -OutputDirectory $OutputPath -PackagePaths @($FullPackagePath, $DeltaPackagePath)

    $existingRelease = $releases | Where-Object { $_.version -eq $Version } | Select-Object -Last 1

    if ($existingRelease)
    {
        $releaseNotes = @($existingRelease.releaseNotes)
    }
    else
    {
        $releaseNotes = @($ReleaseNotes)
        $releases += [PSCustomObject]@{
            version = $Version
            date = (Get-Date -Format "yyyy-MM-dd")
            releaseNotes = $releaseNotes
        }
        ConvertTo-Json -InputObject $releases -Depth 5 | Set-Content $ReleaseLogPath -Encoding UTF8
    }

    if (-not $SkipSftpUpload)
    {
        Send-SftpRelease $FeedPath $ReleaseLogPath
    }

    if (-not $SkipSftpUpload -and -not $SkipModuleUpload)
    {
        & (Join-Path $PSScriptRoot "publish-modules.ps1") -All -ConfigurationPath $ConfigurationPath

        if ($LASTEXITCODE -ne 0)
        {
            throw "Module feed publishing failed with exit code $LASTEXITCODE."
        }
    }

    if (-not $SkipGitRelease)
    {
        Send-GitHubRelease -RepoPath $PSScriptRoot -Version $Version -ReleaseNotes $releaseNotes -AssetPaths @($InstallerPath, $FullPackagePath, $DeltaPackagePath)
    }

    exit 0
}

if ($Version -eq "")
{
    $lastVersion = if ($releases.Count -gt 0) { $releases[-1].version } else { "0.0.0" }
    $cleanLast = $lastVersion -replace '-.*$', ''
    $parts = $cleanLast -split '\.'
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]

    Write-Host ""
    Write-Host "Last version: $lastVersion" -ForegroundColor DarkGray
    Write-Host "Bump options:" -ForegroundColor DarkGray
    Write-Host "  [1] patch  -> $major.$minor.$($patch + 1)" -ForegroundColor DarkGray
    Write-Host "  [2] minor  -> $major.$($minor + 1).0" -ForegroundColor DarkGray
    Write-Host "  [3] major  -> $($major + 1).0.0" -ForegroundColor DarkGray
    Write-Host ""

    if ($Bump -eq "")
    {
        $Bump = Read-Host "Select bump type (1/2/3) or press Enter for patch"

        if ($Bump -eq "")
        {
            $Bump = "1"
        }
    }

    switch ($Bump)
    {
        { $_ -in "1", "patch" }
        {
            $Version = "$major.$minor.$($patch + 1)"
        }
        { $_ -in "2", "minor" }
        {
            $Version = "$major.$($minor + 1).0"
        }
        { $_ -in "3", "major" }
        {
            $Version = "$($major + 1).0.0"
        }
        default
        {
            Write-Host "Invalid bump type." -ForegroundColor Red
            exit 1
        }
    }

    $Channel = Read-Host "Channel (e.g. preview, beta) or press Enter to skip"

    if ($Channel -ne "")
    {
        $Version = "$Version-$Channel"
    }

    Write-Host "Version: $Version" -ForegroundColor Cyan
}

if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?(-[a-zA-Z0-9]+(\.\d+)?)?$')
{
    Write-Host "Invalid version format. Use major.minor.patch[-channel] (e.g. 1.0.0 or 1.0.0-preview)" -ForegroundColor Red
    exit 1
}

if (-not $Local -and
    ($releases | Where-Object { $_.version -eq $Version }))
{
    Write-Host "Version $Version already exists in releases.json" -ForegroundColor Red
    exit 1
}

$dotnetVersion = $Version -replace '-.*$', ''

if (-not $Local -and $ReleaseNotes.Count -eq 0)
{
    Write-Host ""
    Write-Host "Enter release notes (empty line to finish):" -ForegroundColor Cyan

    while ($true)
    {
        $line = Read-Host "  >"

        if ($line -eq "")
        {
            break
        }

        $releaseNotes += $line
    }
}

$OutputPath = if ($Local)
{
    "$PSScriptRoot\Publish\Local\$Version\Assets"
}
else
{
    "$PSScriptRoot\Publish\$Version\Assets"
}
$InstallerPath = "$FeedPath\$PublicInstallerName"
$VelopackInstallerPath = "$FeedPath\$VelopackInstallerName"
$FullPackagePath = "$FeedPath\$VelopackPackId-$Version-full.nupkg"
$DeltaPackagePath = "$FeedPath\$VelopackPackId-$Version-delta.nupkg"
$publishRootPath = Split-Path $OutputPath -Parent

if (Test-Path $publishRootPath)
{
    Remove-Item $publishRootPath -Recurse -Force
}

if (-not $Local -and
    -not $SkipSigning)
{
    Write-Host ""
    Write-Host "Checking Azure signing authentication..." -ForegroundColor Cyan
    Assert-PublishValue "Azure signing endpoint" $AzureSigningEndpoint
    Assert-PublishValue "Azure signing account name" $AzureSigningAccountName
    Assert-PublishValue "Azure signing certificate profile name" $AzureSigningCertProfileName
    Test-AzureSigningAuth
    New-AzureSigningMetadataFile -Path $SigningMetadataPath
    Write-Host "Signing metadata written to $SigningMetadataPath" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Publishing Glance v$Version" -ForegroundColor Cyan
& dotnet publish $ProjectPath -c Release -r win-x64 -o $OutputPath `
    "-p:Platform=x64" `
    "-p:SelfContained=true" `
    "-p:PublishAot=false" `
    "-p:PublishTrimmed=false" `
    "-p:DebugType=None" `
    "-p:DebugSymbols=false" `
    "-p:StripSymbols=true" `
    "-p:IncludeBundledGlanceModules=false" `
    "-p:Version=$dotnetVersion" `
    "-p:AssemblyVersion=1.0.0.0" `
    "-p:FileVersion=$dotnetVersion"

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Assert-ModuleFreeRelease -OutputDirectory $OutputPath

if (-not $Local)
{
    New-ExternalIdentityPackage -Version $Version -OutputDirectory $OutputPath -MetadataPath $SigningMetadataPath -Sign:(-not $SkipSigning)
}

$exePath = Get-ChildItem -Path $OutputPath -Filter "*.exe" | Select-Object -First 1

if ($exePath)
{
    $fileSize = [math]::Round($exePath.Length / 1MB, 2)
    Write-Host "Build successful" -ForegroundColor Green
    Write-Host "Executable : $($exePath.FullName)" -ForegroundColor Green
    Write-Host "Size       : $fileSize MB" -ForegroundColor Green
}

if ($Local)
{
    Write-Host ""
    Write-Host "Local publish completed. Packaging and uploads were skipped." -ForegroundColor Green
    Write-Host "Output     : $OutputPath" -ForegroundColor Green
    return
}

Write-Host "Packaging with Velopack..." -ForegroundColor Cyan

$vpkArgs = @(
    "pack"
    "--packId", $VelopackPackId
    "--packTitle", "Glance"
    "--packVersion", $Version
    "--packDir", $OutputPath
    "--outputDir", $FeedPath
    "--mainExe", "Glance.exe"
)

if (-not $SkipSigning)
{
    $vpkArgs += @("--azureTrustedSignFile", $SigningMetadataPath)
}

& vpk @vpkArgs

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Velopack packaging failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

if (-not (Test-Path $VelopackInstallerPath))
{
    Write-Host "Installer was not found: $VelopackInstallerPath" -ForegroundColor Red
    exit 1
}

if ($VelopackInstallerPath -ne $InstallerPath)
{
    Copy-Item $VelopackInstallerPath $InstallerPath -Force
}

if (-not (Test-Path $FullPackagePath))
{
    throw "The full update package was not created: $FullPackagePath"
}

if (-not (Test-Path $DeltaPackagePath))
{
    throw "The delta update package was not created: $DeltaPackagePath"
}

Assert-ModuleFreeRelease -OutputDirectory $OutputPath -PackagePaths @($FullPackagePath, $DeltaPackagePath)

if (-not $SkipMicrosoftStore)
{
    Write-Host ""
    Write-Host "Publishing Microsoft Store package..." -ForegroundColor Cyan

    $storeArguments = @{
        Version              = $Version
        InputDirectory       = $OutputPath
        ProductId            = $MicrosoftStoreProductId
        IdentityName         = $MicrosoftStoreIdentityName
        Publisher            = $MicrosoftStorePublisher
        PublisherDisplayName = $MicrosoftStorePublisherDisplayName
        TenantId             = $MicrosoftStoreTenantId
        SellerId             = $MicrosoftStoreSellerId
        ClientId             = $MicrosoftStoreClientId
        ClientSecret         = $MicrosoftStoreClientSecret
        FlightId             = $MicrosoftStoreFlightId
    }

    if ($MicrosoftStoreDraft)
    {
        $storeArguments.NoCommit = $true
    }

    if ($MicrosoftStorePackageOnly)
    {
        $storeArguments.PackageOnly = $true
    }

    Send-MicrosoftStoreRelease @storeArguments
}

$newEntry = [PSCustomObject]@{
    version = $Version
    date = (Get-Date -Format "yyyy-MM-dd")
    releaseNotes = $releaseNotes
}

$releases += $newEntry
ConvertTo-Json -InputObject $releases -Depth 5 | Set-Content $ReleaseLogPath -Encoding UTF8

Write-Host "Packaged successfully to $FeedPath" -ForegroundColor Green
Write-Host "Version    : $Version" -ForegroundColor Green
Write-Host "Release log updated: $ReleaseLogPath" -ForegroundColor Green

if (-not $SkipSftpUpload)
{
    Send-SftpRelease $FeedPath $ReleaseLogPath
}

if (-not $SkipSftpUpload -and -not $SkipModuleUpload)
{
    Write-Host ""
    Write-Host "Publishing complete module feed..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "publish-modules.ps1") -All -ConfigurationPath $ConfigurationPath

    if ($LASTEXITCODE -ne 0)
    {
        throw "Module feed publishing failed with exit code $LASTEXITCODE."
    }
}

if (-not $SkipGitRelease)
{
    Send-GitHubRelease -RepoPath $PSScriptRoot -Version $Version -ReleaseNotes $releaseNotes -AssetPaths @($InstallerPath, $FullPackagePath, $DeltaPackagePath)
}
