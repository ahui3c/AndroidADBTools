# Android ADB 快速工具

[繁體中文](README.md) · [English](README.en.md)

<p align="center">
  <img src="assets/app-icon.png" width="128" alt="Android ADB 快速工具圖示">
</p>

一套免安裝的 Windows 圖形化 ADB 工具，協助使用者快速確認 Android 裝置連線、批次安裝 APK、調整常用系統設定、擷取畫面與備份手機相片資料。

目前版本：**v1.15.6**

## 主要功能

- 檢查 `adb.exe`、USB 偵錯授權、離線與未授權狀態。
- 支援 USB 與 Wi-Fi 無線偵錯連線，內建可複製的連線教學。
- 建立多組「常用 APK 安裝」清單，一鍵依序安裝並顯示每個 APK 的結果。
- 自動掃描程式旁 `APKs` 目錄中的子資料夾，建立不可誤刪的同步安裝組合。
- 支援拖放 APK 到常用組合，或拖放一個／多個 APK 立即快速安裝。
- 讀取與即時調整手機亮度，支援滑桿、數值、`+`／`-` 鍵及滑鼠滾輪。
- 快速設定自動亮度、10 分鐘關屏、最長關屏時間及充電時保持螢幕開啟。
- 各項快速設定獨立執行並讀回驗證；單項失敗不影響其他設定。
- 快速調整媒體音量、在手機開啟網址、擷取手機畫面並儲存 PNG。
- 下載手機 `DCIM`、`Pictures`、`Picture` 內的檔案，保留目錄結構並壓縮成 ZIP。
- 下載前先取得檔案大小，可略過超過自訂上限的單一檔案（預設 2 GB）。
- 支援 Per-Monitor V2 高 DPI、視窗大小記憶與 4K 顯示器縮放。

## 系統需求

- Windows 10 或 Windows 11
- .NET Framework 4.8
- Android Platform Tools 中的 `adb.exe`
- 手機已開啟「開發人員選項」及「USB 偵錯」或「無線偵錯」

## 取得 Android SDK Platform-Tools

`adb.exe` 包含在 Google 官方的 Android SDK Platform-Tools 中：

- 官方下載頁：[SDK Platform-Tools release notes](https://developer.android.com/tools/releases/platform-tools)
- 進入頁面後選擇 **Download SDK Platform-Tools for Windows**，閱讀並同意條款後下載 ZIP。
- 解壓縮後，`adb.exe` 位於 `platform-tools` 資料夾內；在本工具按「選擇 adb.exe」並指定該檔案即可。
- 若已安裝 Android Studio，也可透過 **SDK Manager > SDK Tools > Android SDK Platform-Tools** 安裝或更新；程式通常會自動找到預設 SDK 位置。

建議使用官方頁面提供的最新版本。Google 表示 Platform-Tools 向下相容舊版 Android，因此一般不需要另外尋找舊版 ADB。

## 下載與使用

1. 到 [Releases](https://github.com/ahui3c/AndroidADBTools/releases) 下載最新版 `AndroidADBTools.exe` 或完整 ZIP。
2. 執行 `AndroidADBTools.exe`。
3. 若程式沒有自動找到 ADB，按「選擇 adb.exe」，指定 Android SDK 的 `platform-tools\adb.exe`。
4. 連接並授權手機後按「重新檢查」。

程式會依序搜尋：已儲存路徑、程式旁的 `adb.exe`、`platform-tools\adb.exe`、Android SDK 預設位置及系統 `PATH`。

## APK 資料夾同步

可在程式旁建立以下結構：

```text
APKs/
├─ 常用工具/
│  ├─ app1.apk
│  └─ app2.apk
└─ 測試程式/
   └─ test.apk
```

程式啟動時會將每個子資料夾建立為一個安裝組合；點選時會重新掃描 APK 內容。資料夾同步組合會以資料夾圖示標示，名稱與內容直接由檔案系統管理。

## 手機資料下載

- 掃描 `/sdcard/DCIM`、`/sdcard/Pictures` 與 `/sdcard/Picture`。
- 在手機端先建立路徑與大小清單，再依設定決定是否傳輸。
- ZIP 名稱格式為 `手機型號_yyyyMMdd-HHmmss.zip`。
- 大小上限是針對「單一檔案」，不是整個備份的總大小。
- USB 與 Wi-Fi ADB 皆可使用；大型備份建議使用 USB。

## 從原始碼建置

在 PowerShell 執行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Build.ps1
```

輸出檔案位於 `dist\AndroidADBTools.exe`。建置腳本使用 Windows 內建的 .NET Framework C# 編譯器，不需要另外安裝 .NET SDK。

## 設定儲存位置

使用者設定儲存在：

```text
%LOCALAPPDATA%\AndroidADBTools\settings.json
```

內容包含 ADB 路徑、APK 組合、組合順序、視窗大小、下載位置與檔案大小過濾設定。

## 作者

- 廖阿輝
- 郵件：[chehui@gmail.com](mailto:chehui@gmail.com)
- 網站：[https://ahui3c.com](https://ahui3c.com)
