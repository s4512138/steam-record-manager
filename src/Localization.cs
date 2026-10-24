using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SteamRecordManager {
public static class L {
    public static readonly string[] Codes={"zh-TW","zh-CN","en"};
    public static readonly string[] Languages={"繁體中文","简体中文","English"};
    public static int Index=0;
    static readonly Dictionary<string,string[]> Table=new Dictionary<string,string[]>();
    static void Add(string tw,string cn,string en){Table.Add(tw,new[]{tw,cn,en});}
    static L(){
        Add("Steam Record 空間管理員 1.2","Steam Record 空间管理器 1.2","Steam Record Manager 1.2");
        Add("Steam Record 空間管理員","Steam Record 空间管理器","Steam Record Manager");
        Add("STEAM RECORD  /  錄影空間管理","STEAM RECORD  /  录像空间管理","STEAM RECORD  /  STORAGE MANAGER");
        Add("選擇資料夾","选择文件夹","Browse folder");
        Add("選擇含 video、clips 的錄影根目錄","选择含 video、clips 的录像根目录","Select the recording root containing video and clips");
        Add("重新掃描","重新扫描","Rescan");
        Add("選擇 Steam 錄影資料夾後掃描；不會自動清理。","选择 Steam 录像文件夹后扫描；不会自动清理。","Select your Steam recording folder to scan. No automatic cleanup.");
        Add("搜尋遊戲／App ID","搜索游戏／App ID","Search game / App ID");
        Add("顯示手動錄影及剪輯","显示手动录像及剪辑","Show manual recordings and clips");
        Add("保護／取消保護","保护／取消保护","Protect / unprotect");
        Add("開啟資料夾","打开文件夹","Open folder");
        Add("選取","选择","Select"); Add("遊戲","游戏","Game"); Add("類型","类型","Type");
        Add("大小 GiB","大小 GiB","Size GiB"); Add("最後寫入（本機）","最后写入（本地）","Last modified (local)");
        Add("保護／狀態","保护／状态","Protection / status"); Add("資料夾","文件夹","Folder");
        Add("背景錄影上限 GiB","后台录像上限 GiB","Background limit GiB");
        Add("保留天數","保留天数","Keep days");
        Add("預覽清理建議","预览清理建议","Preview cleanup");
        Add("取消選取","取消选择","Clear selection");
        Add("匯出報表","导出报告","Export report");
        Add("CSV 報表|*.csv","CSV 报告|*.csv","CSV report|*.csv");
        Add("移至資源回收筒","移至回收站","Move to Recycle Bin");
        Add("永久刪除…","永久删除…","Delete permanently…");
        Add("背景錄影","后台录像","Background"); Add("手動錄影","手动录像","Manual recording"); Add("剪輯","剪辑","Clip"); Add("略過","跳过","Skipped");
        Add("無法管理","无法管理","Unavailable"); Add("已保護","已保护","Protected"); Add("可管理","可管理","Available");
        Add("已辨識 {0:N2} GiB　背景 {1:N2} GiB　手動 {2:N2} GiB　剪輯 {3:N2} GiB　／ {4} 個工作階段，略過 {5} 項","已识别 {0:N2} GiB　后台 {1:N2} GiB　手动 {2:N2} GiB　剪辑 {3:N2} GiB　／ {4} 个会话，跳过 {5} 项","Total {0:N2} GiB   Background {1:N2}   Manual {2:N2}   Clips {3:N2}   / {4} sessions, {5} skipped");
        Add("已選 {0} 項 / {1:N2} GiB\n建議僅作用於搜尋範圍；0 表示停用該規則。","已选 {0} 项 / {1:N2} GiB\n建议仅作用于搜索范围；0 表示停用该规则。","Selected {0} / {1:N2} GiB\nSuggestions apply to search results; 0 disables a rule.");
        Add("工作進行中，請等候完成。","工作进行中，请等待完成。","An operation is in progress. Please wait for it to finish.");
        Add("無法完成","无法完成","Unable to complete");
        Add("正在掃描完整工作階段…","正在扫描完整会话…","Scanning recording sessions…");
        Add("掃描失敗","扫描失败","Scan failed"); Add("掃描未完成。","扫描未完成。","Scan did not complete.");
        Add("確認清理預覽","确认清理预览","Confirm cleanup preview");
        Add("目錄：{0}\r\n{1} 個完整工作階段，{2:N2} GiB\r\n\r\n{3}\r\n\r\nSteam 可能保留舊的清單項目；本工具不修改 gamerecording.pb 與 timelines。\r\n\r\n","目录：{0}\r\n{1} 个完整会话，{2:N2} GiB\r\n\r\n{3}\r\n\r\nSteam 可能保留旧的列表项目；本工具不修改 gamerecording.pb 与 timelines。\r\n\r\n","Folder: {0}\r\n{1} complete sessions, {2:N2} GiB\r\n\r\n{3}\r\n\r\nSteam may retain old library entries. This tool does not modify gamerecording.pb or timelines.\r\n\r\n");
        Add("永久刪除無法復原。備份只含索引，不含錄影。","永久删除无法恢复。备份只含索引，不含录像。","Permanent deletion cannot be undone. Backups contain indexes, not recordings.");
        Add("移至資源回收筒可復原。清空回收筒後才釋放同一磁碟的空間。若系統回收筒不可用，將回報失敗。","移至回收站可恢复。清空回收站后才释放同一磁盘的空间。如果系统回收站不可用，将报告失败。","You can restore recordings from the Recycle Bin. Empty it to free disk space. An unavailable Recycle Bin will cause an error.");
        Add("確認清單後輸入 DELETE，再按確認。","确认列表后输入 DELETE，再按确认。","Review the list, type DELETE, then click Confirm.");
        Add("確認清單後輸入 RECYCLE，再按確認。","确认列表后输入 RECYCLE，再按确认。","Review the list, type RECYCLE, then click Confirm.");
        Add("確認","确认","Confirm");
        Add("請先從 Steam 選單完全離開 Steam，再執行清理。掃描可在 Steam 開啟時使用。","请先从 Steam 菜单完全退出 Steam，再执行清理。扫描可在 Steam 打开时使用。","Fully exit Steam from its menu before cleanup. Scanning is available while Steam is running.");
        Add("路徑已變更，請重新掃描。","路径已更改，请重新扫描。","The folder path changed. Please rescan.");
        Add("正在驗證並清理…","正在验证并清理…","Validating and cleaning up…");
        Add("清理結果","清理结果","Cleanup results"); Add("已停止清理","已停止清理","Cleanup stopped");
        Add("Steam 已重新啟動，取消清理。","Steam 已重新启动，取消清理。","Steam restarted. Cleanup was cancelled.");
        Add("Steam 已啟動。","Steam 已启动。","Steam is running.");
        Add("已完成 ","已完成 ","Completed "); Add(" 項，其餘停止。\n"," 项，其余停止。\n"," item(s); remaining items were stopped.\n"); Add("\n紀錄：","\n记录：","\nLog: ");
        Add("完成 {0} 項，共 {1:N2} GiB。{2}\n索引備份與操作紀錄：{3}","完成 {0} 项，共 {1:N2} GiB。{2}\n索引备份与操作记录：{3}","Completed {0} item(s), {1:N2} GiB. {2}\nIndex backup and operation log: {3}");
        Add("錄影已永久刪除。","录像已永久删除。","Recordings were permanently deleted.");
        Add("請確認內容後自行清空資源回收筒以釋放磁碟空間。","请确认内容后自行清空回收站以释放磁盘空间。","Review the contents, then empty the Recycle Bin yourself to free disk space.");
        Add("索引包含連結。","索引包含链接。","The index contains a link.");
        Add("不支援連結或接合點：","不支持链接或连接点：","Links and junctions are not supported: ");
        Add("請選擇同時含 video 與 clips 的 Steam 錄影根目錄。","请选择同时含 video 与 clips 的 Steam 录像根目录。","Select the Steam recording root containing both video and clips.");
        Add("未知資料夾命名：","未知文件夹命名：","Unrecognized folder name: ");
        Add("包含連結檔案。","包含链接文件。","Contains a linked file.");
        Add("錄影分類與資料夾位置不符。","录像分类与文件夹位置不符。","Recording type does not match the folder location.");
        Add("資料夾超出選定錄影目錄。","文件夹超出选定录像目录。","The folder is outside the selected recording root.");
        Add("錄影已變動或受保護，請重新掃描：","录像已更改或受保护，请重新扫描：","Recording changed or is protected. Please rescan: ");
        Add("錄影仍太新，請等待 10 分鐘：","录像仍太新，请等待 10 分钟：","Recording is too recent. Wait 10 minutes: ");
        Add("資料夾仍存在。","文件夹仍存在。","The folder still exists.");
        Add("無法儲存語言設定：","无法保存语言设置：","Unable to save language preference: ");
        Add("Steam Record 空間管理員 1.3","Steam Record 空间管理器 1.3","Steam Record Manager 1.3");
        Add("同步清理索引（測試版）","同步清理索引（测试版）","Sync index cleanup (beta)");
        Add("修復失效索引…","修复失效索引…","Repair missing references…");
        Add("保留時間軸、成就及剪輯中繼資料；寫入前備份。","保留时间轴、成就及剪辑元数据；写入前备份。","Keep timelines, achievements and clip metadata; back up before writing.");
        Add("移除 {0} 個錄影參照、{1} 個剪輯參照；保留全部 {2} 條時間軸。","移除 {0} 个录像引用、{1} 个剪辑引用；保留全部 {2} 条时间轴。","Remove {0} recording references and {1} clip references; keep all {2} timelines.");
        Add("索引格式無法確認，已停止；未猜測修改。","索引格式无法确认，已停止；未猜测修改。","Unrecognized index format. Stopped without guessing changes.");
        Add("錄影含雲端參照，無法自動同步索引：","录像含云端引用，无法自动同步索引：","Recording has remote references; automatic index cleanup is unavailable: ");
        Add("索引已變動，請重新掃描；尚未套用索引修改。","索引已更改，请重新扫描；尚未应用索引修改。","The index changed. Rescan; index changes have not been applied.");
        Add("索引寫入後驗證失敗，請查看備份與操作紀錄。","索引写入后验证失败，请查看备份与操作记录。","Index verification after writing failed. Check backups and operation logs.");
        Add("將同步移除已刪錄影的索引參照；保留時間軸。索引寫入前會備份，尚未經你的 Steam 重新載入驗證。","将同步移除已删录像的索引引用；保留时间轴。索引写入前会备份，尚未经你的 Steam 重新加载验证。","Index references for deleted recordings will be removed; timelines will be kept. The index is backed up before writing. Reloading in your Steam client has not been verified.");
        Add("沒有可確認失效的本機錄影或剪輯參照；未修改索引。","没有可确认失效的本地录像或剪辑引用；未修改索引。","No confirmed missing local recording or clip references were found. Index unchanged.");
        Add("只清除來源資料夾確實不存在的本機參照，不會刪除影片。保留所有時間軸、成就與雲端參照。這是副本測試通過、尚未經 Steam 實機載入驗證的功能。","只清除源文件夹确实不存在的本地引用，不会删除视频。保留所有时间轴、成就与云端引用。这是副本测试通过、尚未经 Steam 实机加载验证的功能。","Only local references whose source folder is missing will be removed. No videos will be deleted. All timelines, achievements and remote references are kept. This feature passed copy-based tests; live Steam loading has not been verified.");
        Add("確認清單後輸入 REPAIR，再按確認。","确认列表后输入 REPAIR，再按确认。","Review the list, type REPAIR, then click Confirm.");
        Add("索引或資料夾已變動，請重新預覽。","索引或文件夹已更改，请重新预览。","The index or folders changed. Preview again.");
        Add("索引備份與操作紀錄：","索引备份与操作记录：","Index backup and operation log: ");
        Add("目錄：{0}\r\n{1} 個完整工作階段，{2:N2} GiB\r\n\r\n{3}\r\n\r\n{4}\r\n\r\n","目录：{0}\r\n{1} 个完整会话，{2:N2} GiB\r\n\r\n{3}\r\n\r\n{4}\r\n\r\n","Folder: {0}\r\n{1} complete sessions, {2:N2} GiB\r\n\r\n{3}\r\n\r\n{4}\r\n\r\n");
        Add("Steam 可能保留舊的清單項目；本工具不修改 gamerecording.pb 與 timelines。","Steam 可能保留旧的列表项目；本工具不修改 gamerecording.pb 与 timelines。","Steam may retain old library entries. This tool does not modify gamerecording.pb or timelines.");
        Add("Steam Record 空間管理員 1.4.0-preview.1","Steam Record 空间管理器 1.4.0-preview.1","Steam Record Manager 1.4.0-preview.1");
        Add("只支援本機磁碟的錄影資料夾。","只支持本地磁盘的录像文件夹。","Only recording folders on local drives are supported.");
        Add("報表不可儲存在錄影根目錄內。","报告不可保存在录像根目录内。","Reports cannot be saved inside the recording root.");
        Add("請先完成掃描再匯出報表。","请先完成扫描再导出报告。","Complete a scan before exporting a report.");
    }
    public static string Canonical(string text){if(text==null)return "";if(Table.ContainsKey(text))return text;foreach(var pair in Table)if(pair.Value.Contains(text))return pair.Key;return text;}
    public static string T(string text){string[] values;return Table.TryGetValue(Canonical(text),out values)?values[Index]:text;}
    public static string Error(string text){
        if(text==null)return "";
        foreach(var pair in Table.OrderByDescending(p=>p.Key.Length))foreach(var value in pair.Value.Distinct())
            if(value.Length>0 && text.StartsWith(value,StringComparison.Ordinal))return pair.Value[Index]+text.Substring(value.Length);
        return text;
    }
    public static void Load(string path){Index=0;try{int i=Array.IndexOf(Codes,File.ReadAllText(path).Trim());if(i>=0)Index=i;}catch{}}
    public static void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));SafeFiles.CheckFile(path);string temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(stream)){writer.Write(Codes[Index]);writer.Flush();stream.Flush(true);}SafeFiles.CheckFile(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
    public static void TestCatalog(){foreach(var pair in Table){if(pair.Value.Length!=3 || pair.Value.Any(String.IsNullOrWhiteSpace))throw new Exception("Missing translation: "+pair.Key);foreach(var value in pair.Value){var expected=System.Text.RegularExpressions.Regex.Matches(pair.Key,@"\{\d+[^}]*\}").Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value).OrderBy(v=>v);var actual=System.Text.RegularExpressions.Regex.Matches(value,@"\{\d+[^}]*\}").Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value).OrderBy(v=>v);if(!expected.SequenceEqual(actual))throw new Exception("Translation placeholders differ: "+pair.Key);}}}
}
}
