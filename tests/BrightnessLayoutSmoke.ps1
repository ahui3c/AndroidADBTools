$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $projectRoot 'dist\AndroidADBTools.exe'
$testBuildFolder = Join-Path $projectRoot 'build\tests'
$testExe = Join-Path $testBuildFolder 'BrightnessLayoutSmoke.exe'
$imagePath = Join-Path $projectRoot 'outputs\brightness-layout-smoke.png'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}

New-Item -ItemType Directory -Force -Path $testBuildFolder | Out-Null
& $csc /nologo /target:exe /reference:System.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /out:$testExe `
    (Join-Path $PSScriptRoot 'BrightnessLayoutSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw '亮度版面測試編譯失敗。' }

& $testExe $exePath $imagePath
if ($LASTEXITCODE -ne 0) { throw '亮度版面測試失敗。' }
