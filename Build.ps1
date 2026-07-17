$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw '找不到 Windows 內建 C# 編譯器。'
}
$outDir = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
& $csc /nologo /target:winexe /optimize+ /platform:anycpu `
    /win32manifest:"$root\app.manifest" `
    /win32icon:"$root\assets\app-icon.ico" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    /out:"$outDir\AndroidADBTools.exe" "$root\Program.cs"
if ($LASTEXITCODE -ne 0) { throw '編譯失敗。' }
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $outDir -Force
Copy-Item -LiteralPath (Join-Path $root 'README.en.md') -Destination $outDir -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $outDir -Force
Copy-Item -LiteralPath (Join-Path $root 'App.config') -Destination (Join-Path $outDir 'AndroidADBTools.exe.config') -Force
Write-Host "完成：$outDir\AndroidADBTools.exe"
