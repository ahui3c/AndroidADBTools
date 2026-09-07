param(
    [string]$AdbPath = 'C:\ADBtools\adb.exe'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Add-Type -Path (Join-Path $root 'AdbPortSelector.cs')

$selection = [AndroidADBTools.AdbPortSelector]::Select()
$excluded = netsh interface ipv4 show excludedportrange protocol=tcp | Out-String
$defaultExcluded = $false
foreach ($line in ($excluded -split "`r?`n")) {
    if ($line -match '^\s*(\d+)\s+(\d+)' -and
        5037 -ge [int]$matches[1] -and 5037 -le [int]$matches[2]) {
        $defaultExcluded = $true
    }
}

if ($defaultExcluded -and (-not $selection.UsedFallback -or $selection.Port -eq 5037)) {
    throw '5037 位於 Windows 排除範圍，但選擇器沒有切換連接埠。'
}
if (-not [AndroidADBTools.AdbPortSelector]::IsDaemonStartupFailure(
    'cannot bind to 127.0.0.1:5037 (10013)')) {
    throw '未正確辨識 Windows Socket 10013。'
}
if (-not [AndroidADBTools.AdbPortSelector]::IsDaemonStartupFailure(
    'could not read ok from ADB Server; failed to start daemon')) {
    throw '未正確辨識 ADB daemon 啟動失敗。'
}

if (Test-Path -LiteralPath $AdbPath) {
    $existing = Get-NetTCPConnection -LocalPort $selection.Port -State Listen -ErrorAction SilentlyContinue
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & $AdbPath -P $selection.Port devices -l 2>&1 | Out-String
    $adbExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference
    if ($adbExitCode -ne 0) { throw "ADB 替代連接埠測試失敗：$output" }
    $listener = Get-NetTCPConnection -LocalPort $selection.Port -State Listen -ErrorAction SilentlyContinue
    if (-not $listener) { throw "ADB 未在 TCP $($selection.Port) 啟動。" }
    if (-not $existing) { & $AdbPath -P $selection.Port kill-server 2>&1 | Out-Null }
}

Write-Host "ADB_PORT_SELECTOR_SMOKE_OK port=$($selection.Port) fallback=$($selection.UsedFallback)"
