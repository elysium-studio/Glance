param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$archiveUri = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n8.1-latest-win64-lgpl-8.1.zip'
$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('glance-ffmpeg-' + [Guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $temporaryRoot 'ffmpeg.zip'
$extractPath = Join-Path $temporaryRoot 'extract'

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Invoke-WebRequest -Uri $archiveUri -OutFile $archivePath
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
    $executable = Get-ChildItem -LiteralPath $extractPath -Filter 'ffmpeg.exe' -Recurse | Select-Object -First 1

    if ($null -eq $executable) {
        throw 'The FFmpeg archive did not contain ffmpeg.exe.'
    }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    Copy-Item -LiteralPath $executable.FullName -Destination (Join-Path $OutputDirectory 'ffmpeg.exe') -Force
    $license = Get-ChildItem -LiteralPath $extractPath -Filter 'LICENSE.txt' -Recurse | Select-Object -First 1

    if ($null -ne $license) {
        Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $OutputDirectory 'LICENSE.txt') -Force
    }
}
finally {
    $resolvedTemporaryRoot = [IO.Path]::GetFullPath($temporaryRoot)
    $resolvedSystemTemporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())

    if ($resolvedTemporaryRoot.StartsWith($resolvedSystemTemporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTemporaryRoot)) {
        Remove-Item -LiteralPath $resolvedTemporaryRoot -Recurse -Force
    }
}
