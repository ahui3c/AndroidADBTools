$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $projectRoot 'InstalledAppParser.cs')

$sample = @'
package:/data/app/~~AbCdEf==/com.example.alpha-XyZ123==/base.apk=com.example.alpha installer=com.android.vending
package:/data/app/com.example.beta/base.apk=com.example.beta installer=null
package:/data/app/com.example.alpha/new-base.apk=com.example.alpha installer=com.sec.android.app.samsungapps
warning: unrelated output
package:/invalid.apk=bad package installer=null
'@

$apps = [AndroidADBTools.InstalledAppParser]::Parse($sample)
if ($apps.Count -ne 2) { throw "解析數量錯誤：$($apps.Count)" }
if ($apps[0].PackageName -ne 'com.example.alpha') { throw '應用程式未依套件名稱排序。' }
if ($apps[0].ApkPath -ne '/data/app/com.example.alpha/new-base.apk') { throw '重複套件未採用最新資料。' }
if ($apps[0].InstallerPackage -ne 'com.sec.android.app.samsungapps') { throw '安裝來源解析錯誤。' }
if ($apps[1].InstallerPackage -ne '') { throw 'installer=null 應正規化為空值。' }
if (-not [AndroidADBTools.InstalledAppParser]::IsValidPackageName('com.example.valid_app')) {
    throw '合法套件名稱驗證失敗。'
}
if ([AndroidADBTools.InstalledAppParser]::IsValidPackageName('com.example.bad;rm')) {
    throw '危險套件名稱未被拒絕。'
}

Write-Output 'INSTALLED_APP_PARSER_SMOKE_OK'
