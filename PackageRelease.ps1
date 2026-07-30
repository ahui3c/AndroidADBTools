param(
    [string]$Version = '2.0.2',
    [string]$PlatformToolsZip = '',
    [string]$ArgyllBinaryZip = '',
    [string]$ArgyllSourceZip = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$releaseRoot = Join-Path $root 'release'

if ([string]::IsNullOrWhiteSpace($PlatformToolsZip)) {
    $PlatformToolsZip = Join-Path $releaseRoot 'vendor\platform-tools-latest-windows.zip'
}
if ([string]::IsNullOrWhiteSpace($ArgyllBinaryZip)) {
    $ArgyllBinaryZip = Join-Path $releaseRoot 'vendor\Argyll_V3.5.0_win64_exe.zip'
}
if ([string]::IsNullOrWhiteSpace($ArgyllSourceZip)) {
    $ArgyllSourceZip = Join-Path $releaseRoot 'vendor\Argyll_V3.5.0_src.zip'
}

$PlatformToolsZip = [System.IO.Path]::GetFullPath($PlatformToolsZip)
$ArgyllBinaryZip = [System.IO.Path]::GetFullPath($ArgyllBinaryZip)
$ArgyllSourceZip = [System.IO.Path]::GetFullPath($ArgyllSourceZip)
if (-not (Test-Path -LiteralPath $PlatformToolsZip -PathType Leaf)) {
    throw "找不到 Android Platform-Tools ZIP：$PlatformToolsZip"
}
if (-not (Test-Path -LiteralPath $ArgyllBinaryZip -PathType Leaf)) {
    throw "找不到 ArgyllCMS Windows ZIP：$ArgyllBinaryZip"
}
if (-not (Test-Path -LiteralPath $ArgyllSourceZip -PathType Leaf)) {
    throw "找不到 ArgyllCMS 對應原始碼 ZIP：$ArgyllSourceZip"
}

& (Join-Path $root 'Build.ps1')
if ($LASTEXITCODE -ne 0) {
    throw 'AndroidADBTools 編譯失敗。'
}

$exe = Join-Path $root 'dist\AndroidADBTools.exe'
$fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion
if ($fileVersion -ne "$Version.0") {
    throw "程式版本 $fileVersion 與打包版本 $Version 不一致。"
}

$standardName = "AndroidADBTools-v$Version"
$completeName = "$standardName-complete"
$standardDir = Join-Path $releaseRoot $standardName
$completeDir = Join-Path $releaseRoot $completeName
$standardZip = Join-Path $releaseRoot "$standardName.zip"
$completeZip = Join-Path $releaseRoot "$completeName.zip"
$tempDir = Join-Path $releaseRoot "package-temp-v$Version"

foreach ($target in @($standardDir, $completeDir, $tempDir)) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
foreach ($target in @($standardZip, $completeZip)) {
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Force
    }
}

New-Item -ItemType Directory -Force -Path $standardDir, $completeDir, $tempDir | Out-Null

function Copy-CommonPackageFiles {
    param([string]$Destination)

    New-Item -ItemType Directory -Force -Path (Join-Path $Destination 'APKs') | Out-Null
    Copy-Item -LiteralPath $exe -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $root 'App.config') -Destination (Join-Path $Destination 'AndroidADBTools.exe.config') -Force
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $root 'README.en.md') -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $root 'CHANGELOG.md') -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $Destination -Force
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE-MIT-LEGACY') -Destination $Destination -Force
    $screenshotsSource = Join-Path $root 'docs\screenshots'
    if (Test-Path -LiteralPath $screenshotsSource -PathType Container) {
        $docsDestination = Join-Path $Destination 'docs'
        New-Item -ItemType Directory -Force -Path $docsDestination | Out-Null
        Copy-Item -LiteralPath $screenshotsSource -Destination $docsDestination -Recurse -Force
    }
}

Copy-CommonPackageFiles -Destination $standardDir
Copy-CommonPackageFiles -Destination $completeDir

$platformExtract = Join-Path $tempDir 'platform'
$argyllExtract = Join-Path $tempDir 'argyll'
Expand-Archive -LiteralPath $PlatformToolsZip -DestinationPath $platformExtract -Force
Expand-Archive -LiteralPath $ArgyllBinaryZip -DestinationPath $argyllExtract -Force

$platformSource = Join-Path $platformExtract 'platform-tools'
$argyllSource = Join-Path $argyllExtract 'Argyll_V3.5.0'
$requiredPlatformFiles = @('adb.exe', 'AdbWinApi.dll', 'AdbWinUsbApi.dll', 'NOTICE.txt', 'source.properties')
foreach ($name in $requiredPlatformFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $platformSource $name) -PathType Leaf)) {
        throw "Platform-Tools 發行包缺少必要檔案：$name"
    }
}
if ((Get-Content -LiteralPath (Join-Path $platformSource 'source.properties') -Raw) -notmatch 'Pkg\.Revision=37\.0\.0') {
    throw 'Platform-Tools 版本不是預期的 37.0.0。'
}

$spotread = Join-Path $argyllSource 'bin\spotread.exe'
$argyllLicense = Join-Path $argyllSource 'License.txt'
$argyllReadme = Join-Path $argyllSource 'ReadMe.txt'
foreach ($path in @($spotread, $argyllLicense, $argyllReadme)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "ArgyllCMS 發行包缺少必要檔案：$path"
    }
}
if ((Get-Content -LiteralPath $argyllReadme -Raw) -notmatch 'Version 3\.5\.0') {
    throw 'ArgyllCMS 版本不是預期的 3.5.0。'
}

$platformDestination = Join-Path $completeDir 'platform-tools'
New-Item -ItemType Directory -Force -Path $platformDestination | Out-Null
foreach ($name in $requiredPlatformFiles) {
    Copy-Item -LiteralPath (Join-Path $platformSource $name) -Destination $platformDestination -Force
}

$argyllDestination = Join-Path $completeDir 'Argyll'
$argyllBinDestination = Join-Path $argyllDestination 'bin'
New-Item -ItemType Directory -Force -Path $argyllBinDestination | Out-Null
Copy-Item -LiteralPath $spotread -Destination $argyllBinDestination -Force
Copy-Item -LiteralPath $argyllLicense -Destination $argyllDestination -Force
Copy-Item -LiteralPath $argyllReadme -Destination $argyllDestination -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $standardDir,
    $standardZip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $completeDir,
    $completeZip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)

Remove-Item -LiteralPath $tempDir -Recurse -Force

$hashFile = Join-Path $releaseRoot "SHA256SUMS-v$Version.txt"
$hashLines = foreach ($path in @($standardZip, $completeZip, $exe, $ArgyllSourceZip)) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $path
    "$($hash.Hash)  $(Split-Path $path -Leaf)"
}
[System.IO.File]::WriteAllLines($hashFile, $hashLines, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "完成：$standardZip"
Write-Host "完成：$completeZip"
Write-Host "雜湊：$hashFile"
