$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $projectRoot 'dist\AndroidADBTools.exe'
$sourcePath = Join-Path $projectRoot 'Program.cs'

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "找不到建置檔：$exePath"
}

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw '找不到 Windows 內建 C# 編譯器。'
}

$testBuildFolder = Join-Path $projectRoot 'build\tests'
$testExe = Join-Path $testBuildFolder 'SelectedModuleSettingsSmoke.exe'
New-Item -ItemType Directory -Force -Path $testBuildFolder | Out-Null
& $csc /nologo /target:exe /reference:System.dll /reference:System.Web.Extensions.dll `
    /out:$testExe (Join-Path $PSScriptRoot 'SelectedModuleSettingsSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw '功能模組設定測試編譯失敗。' }

& $testExe $exePath
if ($LASTEXITCODE -ne 0) { throw '功能模組設定序列化測試失敗。' }

$source = Get-Content -LiteralPath $sourcePath -Raw
$moduleIds = @(
    'common-apps',
    'quick-transfer',
    'app-management',
    'brightness',
    'quick-settings',
    'device-information',
    'data-download',
    'execution-log'
)

foreach ($moduleId in $moduleIds) {
    if (-not $source.Contains('Name = "' + $moduleId + '"')) {
        throw "缺少固定模組識別名稱：$moduleId"
    }
}

if (-not $source.Contains('mainTabs.SelectedIndexChanged += MainModuleChanged;') -or
    -not $source.Contains('settings.SelectedModule = selectedModule;') -or
    -not $source.Contains('SaveSettings();')) {
    throw '缺少模組切換即時保存流程。'
}

if (-not $source.Contains('settings put system screen_brightness_mode 0') -or
    -not $source.Contains('settings get system screen_brightness_mode') -or
    -not $source.Contains('DisableAutomaticBrightnessAsync(device)')) {
    throw '缺少校正前關閉並驗證自動亮度的流程。'
}

if (-not $source.Contains('儲存本次結果') -or
    -not $source.Contains('快速套用已存結果') -or
    -not $source.Contains('StoreBrightnessCalibration')) {
    throw '缺少依手機保存與快速套用亮度校正結果的流程。'
}

Write-Output 'SELECTED_MODULE_SETTINGS_SMOKE_OK'
