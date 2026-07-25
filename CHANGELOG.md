# 更新紀錄

本專案採用語意化版本格式。完整下載檔與每版發布說明請見 [GitHub Releases](https://github.com/ahui3c/AndroidADBTools/releases)。

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

[2.0.1]: https://github.com/ahui3c/AndroidADBTools/compare/v1.15.6...v2.0.1
[1.15.6]: https://github.com/ahui3c/AndroidADBTools/releases/tag/v1.15.6
