# 更新紀錄

本專案採用語意化版本格式。完整下載檔與每版發布說明請見 [GitHub Releases](https://github.com/ahui3c/AndroidADBTools/releases)。

## [尚未發行]

## [2.0.3] - 2026-08-10

### 資料下載

- 新增不壓縮的資料夾下載模式，直接保留 `DCIM`、`Pictures` 與 `Picture` 的原始目錄結構。
- 資料夾下載會直接寫入使用者選定的位置，不再額外建立手機型號／裝置名稱資料夾。
- 修正高 DPI 或較小視窗中，資料下載頁面捲到底後下載按鈕仍顯示不完整的問題。
- 資料夾模式預設記錄每台手機在各電腦目的地的上次完整下載時間，後續只下載新增或修改的檔案。
- 使用手機型號與穩定裝置 ID 隔離下載紀錄，避免不同手機混用；另提供「重置目前手機紀錄」功能。
- 依大小限制正常略過的檔案不列入傳輸錯誤率；實際複製錯誤率在 10% 以下（含 10%）仍更新時間點，超過 10% 則不更新並顯示警告。
- 資料下載會略過 `.thumbnails` 縮圖快取資料夾，不進行處理，也不列為錯誤。
- 壓縮資料下載頁面的卡片高度、內距與狀態操作區，讓完整下載流程可在單一視窗高度內操作。
- 手機畫面截圖新增「截圖到剪貼簿」，可直接將 PNG 畫面放入 Windows 系統剪貼簿。
- 修正「截圖到剪貼簿」可能誤判 ADB 未回傳 PNG 的問題；儲存截圖與剪貼簿截圖現在共用同一套擷取流程，直接串流失敗時會自動改用裝置暫存截圖並拉回電腦。
- 常用 APK 安裝會記住最後選擇的組合，程式下次啟動時自動恢復該組合。
- 常用 APK 組合清單新增類型圖示：自建組合使用藍色資料夾，資料夾同步組合維持黃色資料夾，方便快速區分。

## [2.0.2] - 2026-07-30

### 發行包

- Release 改為同時提供標準版 `AndroidADBTools-v2.0.2.zip` 與 `AndroidADBTools-v2.0.2-complete.zip`。
- 標準版只包含 AndroidADBTools；Complete 版額外內含 Android ADB 37.0.0 與 ArgyllCMS 3.5.0 `spotread.exe`，解壓縮後可由程式自動偵測。
- Complete 版保留 Android Platform-Tools NOTICE、ArgyllCMS AGPLv3 授權與對應原始碼資訊。

### 亮度量測與說明

- 自動亮度說明不再綁定單一感測器，改為通用的 ArgyllCMS 相容量測設備說明。
- 將功能標題簡化為「全自動調整設備亮度」，並重寫操作說明。
- 尚未指定工具路徑時，自動尋找 Complete 版內附的 ADB 與 `spotread.exe`，填入並保存可直接使用的執行位置。
- 新增「相容量測設備」視窗，列出常見 Calibrite／X-Rite、Datacolor、Klein、JETI、ColorHug 等系列，並連結官方完整清單。
- 將「試量測」統一改名為「設備測試」，並縮短 spotread 選擇按鈕文字，避免高 DPI 下換行。
- 更新中英文 README 與完整教學，加入兩種下載包差異、軟體畫面、spotread 相容硬體表格、驅動注意事項及第三方授權資訊。

## [2.0.1] - 2026-07-25

### 主要改版

- 全面更新主介面與高 DPI 排版，加入裝置選擇器並改善 4K 顯示器使用體驗。
- 新增多裝置管理；可指定主要操作裝置，或將 APK 一次安裝到所有已連線裝置。
- 新增完整 Wi-Fi 無線偵錯管理，支援配對碼、偵錯 Port、mDNS 搜尋、裝置紀錄與啟動時自動重連。

### APK 與檔案傳輸

- 「常用 APK 安裝」支援組合拖曳排序、雙擊改名、完整路徑提示與資料夾同步組合。
- 「快速安裝／傳輸」改為雙拖放區，APK、一般檔案與整個資料夾可直接拖入。
- 快速傳輸可選擇 `/sdcard/Download/`、`/sdcard/DCIM/`、`/sdcard/Pictures/` 或 `/sdcard/`，並保留資料夾結構。
- 改善逐項傳輸結果、錯誤紀錄與完成摘要。

### 亮度與色度計

- 改善手動亮度介面、亮度上限偵測與套用結果顯示。
- 移除容易誤觸的滑鼠滾輪亮度調整，保留滑桿、數值與 `+`／`-` 鍵。
- 新增 ArgyllCMS `spotread` 外接色度計量測，以及依目標 nit 自動調整 Android 亮度的閉迴路模式。
- 新增白色測試圖、CCSS／CCMX 修正檔、量測進度、逾時與儀器狀態診斷。
- 可辨識 Logitech LampArray 等 HID 占用，以及環境光擴散蓋位置錯誤並顯示明確提示。

### 其他工具

- 新增手機資料下載與 ZIP 打包，保留 DCIM／Pictures 目錄結構並可略過超大單檔。
- 新增媒體音量快速調整、手機開啟網址與畫面截圖。
- 快速設定改為逐項套用並讀回驗證，單項失敗不影響其他項目。
- 更新繁體中文、英文 README 與完整功能教學。

### 授權

- v1.16.0 起採用 GNU AGPL v3；v1.15.6 與更早已發布版本仍維持原有 MIT License。

## [1.15.6] - 2026-07-16

- 第一個完整公開版本。
- 提供 USB／Wi-Fi ADB 連線檢查、APK 組合與拖放安裝。
- 提供亮度、快速設定、截圖與 DCIM／Pictures 資料下載。
- 支援 Per-Monitor V2 高 DPI 與視窗大小記憶。

[尚未發行]: https://github.com/ahui3c/AndroidADBTools/compare/v2.0.3...HEAD
[2.0.3]: https://github.com/ahui3c/AndroidADBTools/compare/v2.0.2...v2.0.3
[2.0.2]: https://github.com/ahui3c/AndroidADBTools/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/ahui3c/AndroidADBTools/compare/v1.15.6...v2.0.1
[1.15.6]: https://github.com/ahui3c/AndroidADBTools/releases/tag/v1.15.6
