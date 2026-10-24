# Steam Record 空間管理員

繁體中文 · [English](README.md)

*程式由 GPT-6.1 Sol 生成*

桌面工具，用來查看及管理 Steam 遊戲錄影佔用的空間。這是獨立社群工具，與 Valve 沒有隸屬關係。

**目前版本 1.4.0 預覽版，提供 Windows 與新的原生 macOS 預覽版。** 索引修改僅 Windows 提供，仍屬測試版、預設不啟用，尚未完成 Steam 重新載入驗證。兩平台清理前都須完全退出 Steam。

## 安裝使用

| 電腦 | 下載 | 解壓縮後 |
| --- | --- | --- |
| Windows 10／11，64 位元 | [Windows 應用程式](https://github.com/s4512138/steam-record-manager/releases/download/v1.4.0-preview.1/SteamRecordManager-windows-x64.zip) | 開啟 `SteamRecordManager.exe` |
| macOS 13 以上，Apple Silicon（M1／M2／M3 等） | [macOS arm64 預覽版](https://github.com/s4512138/steam-record-manager/releases/download/v1.4.0-preview.1/SteamRecordManager-macos-arm64-preview.zip) | 開啟 `Steam Record Manager.app`，也可移到「應用程式」 |
| macOS 13 以上，Intel | [macOS Intel 預覽版](https://github.com/s4512138/steam-record-manager/releases/download/v1.4.0-preview.1/SteamRecordManager-macos-x64-preview.zip) | 開啟 `Steam Record Manager.app`，也可移到「應用程式」 |

Windows 需要 .NET Framework 4.8，目前的 Windows 安裝通常已具備。macOS 使用系統原生框架，使用者不需要安裝 Python、.NET、Xcode 或開發工具。Release 的 `SHA256SUMS.txt` 可核對三個壓縮檔的 SHA-256。

**簽章限制：** Windows 未簽章；macOS 僅有 ad-hoc 簽章，沒有 Apple Developer ID 簽章或 Apple 公證。macOS 可能阻擋下載的程式，不能保證沒有安全提示；請勿停用 Gatekeeper 或其他系統防護。[macOS 預覽版說明](docs/MACOS_PREVIEW.md)

## 兩平台的功能差異

| 功能 | Windows | macOS 預覽版 |
| --- | --- | --- |
| 三語、遊戲名稱、掃描／篩選、保護標記與清理建議 | 有 | 有 |
| 清理完整錄影工作階段 | 回收筒或確認後永久刪除 | 僅移至 macOS 垃圾桶，不提供永久刪除 |
| 索引同步及修復 | 可選測試功能 | 不提供，不修改索引 |
| CSV 報表與報表命令列 | 有 | 尚未提供 |

macOS 是新寫的原生程式，不是 Windows 執行檔改名。功能尚未完全一致；移至垃圾桶後，Steam 可能仍顯示原有索引項目。

## Windows 功能

- 繁中／簡中／English 下拉選單，可記住上次選擇。
- 顯示完整錄影工作階段的遊戲名稱、日期及容量。
- 從本機遊戲安裝清單與 Steam appinfo 快取辨識名稱，不需連網。
- 搜尋、保護重要錄影，依保留天數／背景錄影容量預覽清理建議。
- 預設排除剪輯與手動錄影；清理時使用資源回收筒或再次確認永久刪除。
- 匯出 CSV，對可能被試算表當成公式的欄位加上文字保護。
- 可選的索引同步及失效參照修復，保留時間軸、成就與事件資料。

## 從原始碼建置

需要 64 位元 Windows 10／11、.NET Framework 4.8 與可互動的桌面環境。無須 Python、Steam API 金鑰或系統管理員權限。

在專案根目錄開啟 PowerShell：

```powershell
.\scripts\build.ps1
.\dist\SteamRecordManager.exe
```

建置使用 Windows 已安裝的 .NET Framework 編譯器，不下載套件。執行檔未簽章，請自行審閱原始碼與來源；不要為了執行程式而停用系統防護。

macOS 使用者若要自行建置，需要 Xcode Command Line Tools；一般使用者請直接使用 Release 裡的 `.app`：

```sh
bash scripts/build-macos.sh
open 'dist/Steam Record Manager.app'
```

建置腳本會執行合成資料測試、原生視窗初始化測試及 ad-hoc 簽章驗證。Apple Silicon／Intel 分別由 GitHub 的對應 Mac 主機建置；這不等於已完成實際錄影、垃圾桶復原或 Gatekeeper 的人工驗證。

1. 選擇同時包含 `video`、`clips` 的 Steam 錄影根目錄。
2. 掃描後檢查清單，將重要錄影設為保護。
3. 調整容量及保留天數，按「預覽清理建議」。這只會勾選，不會自動刪檔；`0` 表示停用該規則。搜尋狀態下只計算搜尋範圍，保護及近期項目可能使容量目標無法達成。
4. 完全退出 Steam，檢查最終清單。移至回收筒須輸入 `RECYCLE`，永久刪除須輸入 `DELETE`。

同一磁碟的回收筒須清空後才會釋放空間；若 Windows 因容量或檔案系統限制而提出永久刪除，想保留復原能力時請取消。顯示的 GiB 是邏輯大小，與實際釋放量可能不同。錄影目錄與報表只支援本機磁碟，不支援 UNC、裝置路徑或接合點。

## Windows 索引同步與修復

- **同步清理索引（測試版）**：刪除完整錄影資料夾後，移除對應的本機索引參照；遇到雲端參照會在刪檔前停止。
- **修復失效索引**：只預覽及移除來源資料夾已不存在、且可確認為本機資料的參照，不刪影片。輸入 `REPAIR` 才會執行。
- 每次索引寫入前備份，保留全部時間軸、成就、階段、事件與 `timelines` JSON；未識別欄位保留原始位元組。
- 批次不是交易：影片可能已刪除，但後續索引更新失敗。請查看紀錄，再預覽失效參照修復。
- 從回收筒還原影片不會還原索引。已有新錄影時，勿直接覆蓋舊索引備份。

詳細限制與復原原則見 [索引說明](docs/INDEX_FORMAT.md)。這不代表已完成 Steam 實機相容性驗證。

## Windows 命令列唯讀報表

輸出必須是錄影根目錄外的新檔案；命令列不覆寫現有檔案。

```powershell
.\dist\SteamRecordManager.exe --scan 'D:\Recordings' "$env:TEMP\recordings-new.csv"
.\dist\SteamRecordManager.exe --index-audit 'D:\Recordings' "$env:TEMP\index-audit-new.txt"
```

正式執行檔不含刪除／索引寫入命令列入口，也不包含測試專用的繞過檢查介面。

## 隱私與資安

程式沒有 HTTP、遙測、登入、上傳、自動更新下載或程式注入功能。Windows 設定、保護的資料夾路徑及操作紀錄儲存在 `%LOCALAPPDATA%\SteamRecordManager`；macOS 偏好設定使用 `org.steamrecordmanager.preview` 的 UserDefaults。報表、紀錄及索引備份可能包含遊戲名稱、日期、完整路徑與中繼資料，請勿直接公開。

已做本機原始碼檢查與安全性回歸測試，**不代表保證沒有任何漏洞**。檔案路徑／Steam 程序檢查仍存在與同一使用者程序競爭的時間差。本工具不應以系統管理員權限執行，也不適合作為多人共用機器上的高權限服務。

[資安檢查範圍與修正](docs/SECURITY_REVIEW.md) · [隱私說明](docs/PRIVACY.md) · [弱點回報政策](SECURITY.md)

## 開發與公開

```powershell
.\scripts\test.ps1
```

測試僅修改臨時目錄內新建立的合成資料，並保留測試資料供檢查。GitHub Actions 會在推送後執行 Windows 建置與測試；目前不宣稱遠端 CI 已通過。

`Build desktop release` 工作流程須手動啟動，並明確選擇 `publish` 才會發布。Windows、Apple Silicon 與 Intel 三個建置都通過後，才會從 `main` 建立預覽 Release，上傳三個應用壓縮檔與校驗碼。只有發布工作取得 repository 寫入權限，不含簽章私鑰。

[貢獻說明](CONTRIBUTING.md) · [發布前檢查](docs/PUBLISHING.md) · [更新紀錄](CHANGELOG.md)

MIT 授權，見 [LICENSE](LICENSE)。Steam 商標屬於 Valve。格式參考見 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
