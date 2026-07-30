# 第三方元件與授權聲明

本文件說明 `AndroidADBTools-*-complete.zip` 額外包含的第三方執行檔。沒有 `-complete` 後綴的標準版不包含下列二進位元件。

## Android SDK Platform-Tools / ADB 37.0.0

Complete 版包含以下未修改檔案：

- `platform-tools/adb.exe`
- `platform-tools/AdbWinApi.dll`
- `platform-tools/AdbWinUsbApi.dll`
- `platform-tools/NOTICE.txt`
- `platform-tools/source.properties`

ADB 及其 Windows USB API 元件由 Android Open Source Project 提供，相關原始碼以 Apache License 2.0 授權。完整著作權、第三方授權及聲明保留於 Complete 版的 `platform-tools/NOTICE.txt`。

- 官方版本與下載頁：https://developer.android.com/tools/releases/platform-tools
- ADB 原始碼：https://android.googlesource.com/platform/packages/modules/adb/
- Windows USB API 原始碼：https://android.googlesource.com/platform/development/+/master/host/windows/usb/
- Apache License 2.0：https://www.apache.org/licenses/LICENSE-2.0

Android、Google 與相關標誌為其各自權利人的商標。本專案未獲 Google 背書。

## ArgyllCMS 3.5.0 / spotread

Complete 版包含未修改的 `ArgyllCMS 3.5.0` Windows x64 `spotread.exe`，以及上游發行包中的 `License.txt` 與 `ReadMe.txt`。

ArgyllCMS 由 Graeme W. Gill 開發，主要依 GNU Affero General Public License version 3 授權。`ArgyllCMS` 為其權利人的商標。本專案只散布未修改的 spotread 命令列元件，不宣稱精簡內容是完整的 ArgyllCMS 發行包。

- 官方網站：https://www.argyllcms.com/
- 官方授權與著作權說明：https://www.argyllcms.com/doc/ArgyllDoc.html
- spotread 文件：https://www.argyllcms.com/doc/spotread.html
- 相容設備清單：https://www.argyllcms.com/doc/instruments.html
- Windows 儀器安裝：https://www.argyllcms.com/doc/Installing_MSWindows.html
- GNU AGPLv3：https://www.gnu.org/licenses/agpl-3.0.html

每個包含 ArgyllCMS `spotread.exe` 的 GitHub Release 都會同時附上對應的 `Argyll_V3.5.0_src.zip` 原始碼封存檔。

## 無擔保聲明

第三方元件均依各自授權按「現狀」提供，不附帶任何明示或默示擔保。若本摘要與上游授權文件有差異，以 Complete 發行包內保留的原始授權與 NOTICE 文件為準。
