using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Security.Cryptography;
using Microsoft.VisualBasic.FileIO;

namespace SteamRecordManager {
public class Recording {
    public string Path, Name, App, Game, Kind, Fingerprint, Error;
    public long Bytes; public int Files; public DateTime Date, Modified;
    public bool Pinned;
}
public static class Core {
    public const double GiB = 1073741824.0;
    public static string Full(string p) { string full=System.IO.Path.GetFullPath(p);return full.Length>System.IO.Path.GetPathRoot(full).Length?full.TrimEnd(System.IO.Path.DirectorySeparatorChar):full; }
    public static void NoLink(string p) {
        for(var d = new DirectoryInfo(p); d != null; d = d.Parent)
            if ((d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(L.T("不支援連結或接合點：") + d.FullName);
    }
    public static void RootCheck(string root) {
        root=Full(root);
        if(root.StartsWith(@"\\",StringComparison.Ordinal) || root.Length<3 || !Char.IsLetter(root[0]) || root[1]!=':')throw new IOException(L.T("只支援本機磁碟的錄影資料夾。"));
        NoLink(root);
        if(!Directory.Exists(System.IO.Path.Combine(root,"video")) || !Directory.Exists(System.IO.Path.Combine(root,"clips")))
            throw new IOException(L.T("請選擇同時含 video 與 clips 的 Steam 錄影根目錄。"));
    }
    public static Dictionary<string,string> Games(string root) {
        var result=new Dictionary<string,string>();
        var libs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        libs.Add(System.IO.Path.Combine(System.IO.Path.GetPathRoot(root),"SteamLibrary"));
        try { using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) {
            string steam=key==null?null:key.GetValue("SteamPath") as string;
            if(steam!=null) {
                installs.Add(steam);
            }
        }} catch {}
        foreach(var view in new[]{Microsoft.Win32.RegistryView.Registry32,Microsoft.Win32.RegistryView.Registry64}) try {
            using(var machine=Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine,view))
            using(var key=machine.OpenSubKey(@"SOFTWARE\Valve\Steam")) {
                string steam=key==null?null:key.GetValue("InstallPath") as string;
                if(!String.IsNullOrEmpty(steam))installs.Add(steam);
            }
        }catch{}
        foreach(var process in Process.GetProcessesByName("steam"))try{using(process){installs.Add(System.IO.Path.GetDirectoryName(process.MainModule.FileName));}}catch{}
        foreach(string baseDir in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)})
            if(!String.IsNullOrEmpty(baseDir) && Directory.Exists(System.IO.Path.Combine(baseDir,"Steam")))installs.Add(System.IO.Path.Combine(baseDir,"Steam"));
        foreach(string steam in installs) {
            libs.Add(steam);
            try {
                string vdf=System.IO.Path.Combine(steam,"steamapps","libraryfolders.vdf");
                if(File.Exists(vdf))foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))libs.Add(m.Groups[1].Value.Replace(@"\\",@"\"));
            }catch{}
            try { foreach(var pair in AppInfoNames.Read(System.IO.Path.Combine(steam,"appcache","appinfo.vdf"))) if(!result.ContainsKey(pair.Key))result.Add(pair.Key,pair.Value); }catch{}
        }
        foreach(var lib in libs) try {
            foreach(string f in Directory.GetFiles(System.IO.Path.Combine(lib,"steamapps"),"appmanifest_*.acf")) {
                try {
                string t=File.ReadAllText(f);
                var a=System.Text.RegularExpressions.Regex.Match(t,"\"appid\"\\s+\"(\\d+)\"");
                var n=System.Text.RegularExpressions.Regex.Match(t,"\"name\"\\s+\"([^\"]+)\"");
                if(a.Success && n.Success) result[a.Groups[1].Value]=n.Groups[1].Value;
                }catch{}
            }
        } catch {}
        return result;
    }
    public static Recording Inspect(string path, Dictionary<string,string> games) {
        var r=new Recording {Path=Full(path),Name=System.IO.Path.GetFileName(path),Error=""};
        var m=System.Text.RegularExpressions.Regex.Match(r.Name,@"^(bg|fg|clip)_(\d+)_(\d{8})_(\d{6})$");
        if(!m.Success) throw new IOException(L.T("未知資料夾命名：")+r.Name);
        r.Kind=m.Groups[1].Value=="clip"?"剪輯":m.Groups[1].Value=="fg"?"手動錄影":"背景錄影";
        r.App=m.Groups[2].Value; r.Game=games.ContainsKey(r.App)?games[r.App]:"App "+r.App;
        // Steam's folder clock may be UTC or local depending on client; use modification time for retention.
        DateTime dt; r.Date=DateTime.TryParseExact(m.Groups[3].Value+m.Groups[4].Value,"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out dt)?dt:Directory.GetLastWriteTime(path);
        var signature=new StringBuilder(); r.Modified=Directory.GetLastWriteTimeUtc(path);
        var pending=new Stack<string>(); pending.Push(path);
        var files=new List<FileInfo>();
        while(pending.Count>0) {
            string d=pending.Pop(); NoLink(d);
            foreach(var f in new DirectoryInfo(d).GetFiles()) {
                if((f.Attributes&FileAttributes.ReparsePoint)!=0) throw new IOException(L.T("包含連結檔案。"));
                files.Add(f); r.Bytes+=f.Length; r.Files++;
                if(f.LastWriteTimeUtc>r.Modified) r.Modified=f.LastWriteTimeUtc;
            }
            foreach(string child in Directory.GetDirectories(d)) pending.Push(child);
        }
        foreach(var f in files.OrderBy(f=>f.FullName,StringComparer.Ordinal)) signature.Append(f.FullName.Substring(path.Length)).Append('|').Append(f.Length).Append('|').Append(f.LastWriteTimeUtc.Ticks).Append('\n');
        using(var hash=SHA256.Create()) r.Fingerprint=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(signature.ToString())));
        return r;
    }
    public static List<Recording> Scan(string root) {
        root=Full(root); RootCheck(root); var games=Games(root); var rows=new List<Recording>();
        foreach(string category in new[]{"video","clips"}) {
            string dir=System.IO.Path.Combine(root,category); NoLink(dir);
            foreach(string p in Directory.GetDirectories(dir)) {
                try {
                    var r=Inspect(p,games);
                    if((category=="clips") != (r.Kind=="剪輯")) throw new IOException(L.T("錄影分類與資料夾位置不符。"));
                    rows.Add(r);
                } catch(Exception ex) { rows.Add(new Recording{Path=p,Name=System.IO.Path.GetFileName(p),Game=L.T("無法管理"),Kind="略過",Error=ex.Message,Modified=DateTime.UtcNow}); }
            }
        }
        return rows;
    }
    public static List<Recording> Plan(List<Recording> scope, double capGiB, int days, DateTime now) {
        var sorted=scope.OrderBy(r=>r.Modified).ToList();
        long total=scope.Where(r=>r.Kind=="背景錄影").Sum(r=>r.Bytes); var chosen=new List<Recording>();
        foreach(var r in sorted) {
            if(r.Pinned || r.Kind!="背景錄影" || !String.IsNullOrEmpty(r.Error) || r.Modified>now.AddMinutes(-10)) continue;
            if((days>0 && r.Modified<now.AddDays(-days)) || (capGiB>0 && total>capGiB*GiB)) { chosen.Add(r); total-=r.Bytes; }
        }
        return chosen;
    }
    public static void Validate(string root, Recording r) {
        RootCheck(root); var parent=Full(System.IO.Path.GetDirectoryName(r.Path));
        string expected=Full(System.IO.Path.Combine(root,r.Kind=="剪輯"?"clips":"video"));
        if(!String.Equals(parent,expected,StringComparison.OrdinalIgnoreCase)) throw new IOException(L.T("資料夾超出選定錄影目錄。"));
        NoLink(r.Path);
        var current=Inspect(r.Path,new Dictionary<string,string>());
        if(r.Pinned || !String.IsNullOrEmpty(r.Error) || current.Kind!=r.Kind || current.Name!=r.Name || current.App!=r.App || current.Fingerprint!=r.Fingerprint || current.Modified!=r.Modified || current.Bytes!=r.Bytes)
            throw new IOException(L.T("錄影已變動或受保護，請重新掃描：")+r.Name);
        if(current.Modified>DateTime.UtcNow.AddMinutes(-10)) throw new IOException(L.T("錄影仍太新，請等待 10 分鐘：")+r.Name);
    }
    public static bool SteamRunning() { return Process.GetProcessesByName("steam").Length>0 || Process.GetProcessesByName("steamwebhelper").Length>0; }
    public static void Remove(string root,Recording r,bool permanent) {
        if(SteamRunning())throw new IOException(L.T("Steam 已啟動。"));
        RemoveInternal(root,r,permanent);
    }
#if TESTS
    public static void RemoveForTest(string root,Recording r,bool permanent){RemoveInternal(root,r,permanent);}
#endif
    static void RemoveInternal(string root,Recording r,bool permanent) {
        Validate(root,r);
        if(permanent)Directory.Delete(r.Path,true);
        else FileSystem.DeleteDirectory(r.Path,UIOption.AllDialogs,RecycleOption.SendToRecycleBin,UICancelOption.ThrowException);
        if(Directory.Exists(r.Path))throw new IOException(L.T("資料夾仍存在。"));
    }
    public static string Csv(IEnumerable<Recording> rows) {
        var b=new StringBuilder("Game,AppID,Type,Folder,ModifiedLocal,Bytes,Files,Pinned,Status\r\n");
        foreach(var r in rows) b.AppendLine(String.Join(",",new[]{r.Game,r.App,r.Kind,r.Path,r.Modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),r.Bytes.ToString(),r.Files.ToString(),r.Pinned.ToString(),L.Error(r.Error)}.Select(CsvCell)));
        return b.ToString();
    }
    public static string CsvCell(string value){value=value??"";string trimmed=value.TrimStart();if((trimmed.Length>0 && "=+-@".IndexOf(trimmed[0])>=0) || (value.Length>0 && (value[0]=='\t' || value[0]=='\r' || value[0]=='\n')))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
    public static void OpenFolder(string path){var info=new ProcessStartInfo{FileName=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),Arguments="\""+Full(path)+"\"",UseShellExecute=false};Process.Start(info);}
}
// Read only common/name from Steam's version 39-41 appinfo cache.
// Record lengths and the v41 string table are used to avoid searching arbitrary binary text.
public static class AppInfoNames {
    static string CString(BinaryReader reader,long end) {
        var bytes=new List<byte>();
        while(reader.BaseStream.Position<end && bytes.Count<1048576){byte b=reader.ReadByte();if(b==0)return Encoding.UTF8.GetString(bytes.ToArray());bytes.Add(b);}
        throw new InvalidDataException("Unterminated appinfo string.");
    }
    static void Skip(BinaryReader reader,long count,long end) {
        if(count<0 || reader.BaseStream.Position+count>end)throw new InvalidDataException("Truncated appinfo value.");reader.BaseStream.Position+=count;
    }
    static string ObjectName(BinaryReader reader,long end,string[] keys,string path,int depth) {
        if(depth>64)throw new InvalidDataException("Appinfo nesting too deep.");
        while(reader.BaseStream.Position<end) {
            byte type=reader.ReadByte();if(type==8)return null;
            string key;
            if(keys==null)key=CString(reader,end);
            else {if(reader.BaseStream.Position+4>end)throw new InvalidDataException();int index=reader.ReadInt32();if(index<0 || index>=keys.Length)throw new InvalidDataException("Bad appinfo key.");key=keys[index];}
            string child=path==""?key:path+"/"+key;
            if(type==0){string found=ObjectName(reader,end,keys,child,depth+1);if(found!=null)return found;}
            else if(type==1){string value=CString(reader,end);if(child=="appinfo/common/name" && !String.IsNullOrWhiteSpace(value))return value;}
            else if(type==2 || type==3 || type==4 || type==6)Skip(reader,4,end);
            else if(type==7 || type==10)Skip(reader,8,end);
            else if(type==5){if(reader.BaseStream.Position+2>end)throw new InvalidDataException();Skip(reader,(long)reader.ReadUInt16()*2,end);}
            else throw new InvalidDataException("Unsupported appinfo value type.");
        }
        throw new InvalidDataException("Truncated appinfo object.");
    }
    public static Dictionary<string,string> Read(string filename) {
        var names=new Dictionary<string,string>();
        // Steam can have this file open; metadata is never written by this tool.
        using(var stream=new FileStream(filename,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
        using(var reader=new BinaryReader(stream)) {
            if(stream.Length>134217728)throw new InvalidDataException("Appinfo file exceeds the 128 MiB safety limit.");
            uint magic=reader.ReadUInt32();int version=(int)(magic&255);
            if((magic>>8)!=0x075644 || version<39 || version>41)throw new InvalidDataException("Unsupported appinfo version.");
            reader.ReadUInt32();string[] keys=null;long recordsEnd=stream.Length;
            if(version==41) {
                long table=reader.ReadInt64(),start=stream.Position;
                if(table<start || table>stream.Length-4)throw new InvalidDataException("Bad appinfo string table.");
                recordsEnd=table;stream.Position=table;uint count=reader.ReadUInt32();
                if(count>1000000 || count>stream.Length-stream.Position)throw new InvalidDataException("Bad appinfo string count.");keys=new string[(int)count];
                for(int i=0;i<keys.Length;i++)keys[i]=CString(reader,stream.Length);
                stream.Position=start;
            }
            while(stream.Position+4<=recordsEnd) {
                uint app=reader.ReadUInt32();if(app==0)break;
                if(stream.Position+4>recordsEnd)break;
                uint size=reader.ReadUInt32();long end=stream.Position+size;int header=version>=40?60:40;
                if(size<header || end>recordsEnd)break;
                stream.Position+=header;
                try{string name=ObjectName(reader,end,keys,"",0);if(name!=null)names[app.ToString(CultureInfo.InvariantCulture)]=name;}catch(InvalidDataException){}catch(EndOfStreamException){}
                stream.Position=end;
            }
        }
        return names;
    }
}
public class MainForm:Form {
    TextBox root=new TextBox(),search=new TextBox();
    Label summary=new Label(),status=new Label();
    NumericUpDown cap=new NumericUpDown(),days=new NumericUpDown();
    CheckBox clips=new CheckBox(),syncIndex=new CheckBox();
    ComboBox language=new ComboBox();
    Dictionary<Control,string> captions=new Dictionary<Control,string>();
    DataGridView grid=new DataGridView();
    List<Recording> rows=new List<Recording>(); HashSet<string> pins=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    HashSet<string> selected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    List<Control> actions=new List<Control>(); bool loading=false, busy=false,changingLanguage=false;
    string scannedRoot="",settings,languageFile;
    public MainForm(string preferencesDirectory=null,bool autoScan=true) {
        string preferences=preferencesDirectory??System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SteamRecordManager");
        settings=System.IO.Path.Combine(preferences,"protected.txt");languageFile=System.IO.Path.Combine(preferences,"language.txt");L.Load(languageFile);
        Text=L.T("Steam Record 空間管理員 1.4.0-preview.1"); Width=1320; Height=840; MinimumSize=new Size(1280,720);
        Font=new Font("Microsoft JhengHei UI",10); BackColor=Color.FromArgb(24,31,41); ForeColor=Color.White;
        try { if(File.Exists(settings)) pins=new HashSet<string>(File.ReadAllLines(settings),StringComparer.OrdinalIgnoreCase); } catch {}
        var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(20)};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        Controls.Add(layout);
        var heading=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,200));
        heading.Controls.Add(new Label{Text=L.T("STEAM RECORD  /  錄影空間管理"),Font=new Font(Font.FontFamily,18,FontStyle.Bold),AutoSize=true});
        language.DropDownStyle=ComboBoxStyle.DropDownList;language.Dock=DockStyle.Fill;language.Items.AddRange(L.Languages);language.SelectedIndex=L.Index;language.AccessibleName="Language / 語言 / 语言";heading.Controls.Add(language);layout.Controls.Add(heading,0,0);
        var location=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3}; location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); location.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155)); location.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,115));
        root.Text=""; root.Dock=DockStyle.Fill;
        location.Controls.Add(root); location.Controls.Add(Button(L.T("選擇資料夾"),delegate { using(var d=new FolderBrowserDialog()){ d.Description=L.T("選擇含 video、clips 的錄影根目錄"); if(d.ShowDialog()==DialogResult.OK) root.Text=d.SelectedPath; }}));
        location.Controls.Add(Button(L.T("重新掃描"),async delegate {await Scan();})); layout.Controls.Add(location,0,1);
        summary.Dock=DockStyle.Fill; summary.Text=L.T("選擇 Steam 錄影資料夾後掃描；不會自動清理。"); layout.Controls.Add(summary,0,2);
        var filters=new FlowLayoutPanel{Dock=DockStyle.Fill};
        search.Width=180; search.TextChanged+=delegate{RefreshGrid();}; filters.Controls.Add(new Label{Text=L.T("搜尋遊戲／App ID"),AutoSize=true,Margin=new Padding(0,7,5,0)}); filters.Controls.Add(search);
        clips.Text=L.T("顯示手動錄影及剪輯"); clips.AutoSize=true; clips.CheckedChanged+=delegate{RefreshGrid();}; filters.Controls.Add(clips);
        filters.Controls.Add(Button(L.T("保護／取消保護"),delegate{TogglePin();})); filters.Controls.Add(Button(L.T("開啟資料夾"),delegate{var r=Current(); if(r!=null && Directory.Exists(r.Path)) Core.OpenFolder(r.Path);})); layout.Controls.Add(filters,0,3);
        grid.Dock=DockStyle.Fill; grid.BackgroundColor=BackColor; grid.AutoGenerateColumns=false; grid.AllowUserToAddRows=false; grid.AllowUserToDeleteRows=false; grid.RowHeadersVisible=false; grid.MultiSelect=false; grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect; grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
        grid.DefaultCellStyle.BackColor=Color.FromArgb(36,46,60); grid.DefaultCellStyle.ForeColor=Color.White; grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(36,100,145); grid.EnableHeadersVisualStyles=false; grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(46,60,77); grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
        grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Pick",HeaderText=L.T("選取"),Width=55});
        foreach(var col in new[]{L.T("遊戲"),L.T("類型"),L.T("大小 GiB"),L.T("最後寫入（本機）"),L.T("保護／狀態"),L.T("資料夾")}) grid.Columns.Add(new DataGridViewTextBoxColumn{Name=col,HeaderText=col,ReadOnly=true,SortMode=DataGridViewColumnSortMode.NotSortable});
        grid.Columns[1].Width=220;grid.Columns[2].Width=145;grid.Columns[3].Width=95;grid.Columns[4].Width=175;grid.Columns[5].Width=155; grid.Columns[6].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
        grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        grid.CellValueChanged+=delegate(object s,DataGridViewCellEventArgs e){ if(loading || e.RowIndex<0 || e.ColumnIndex!=0)return; var g=grid.Rows[e.RowIndex]; var r=g.Tag as Recording; if(r==null)return; if(r.Pinned || !String.IsNullOrEmpty(r.Error)){loading=true;g.Cells[0].Value=false;loading=false;selected.Remove(r.Path);} else if(Convert.ToBoolean(g.Cells[0].Value)) selected.Add(r.Path); else selected.Remove(r.Path); UpdateStatus();};
        layout.Controls.Add(grid,0,4);
        var policy=new FlowLayoutPanel{Dock=DockStyle.Fill}; cap.Maximum=100000;cap.Value=100;cap.Width=72;days.Maximum=36500;days.Value=30;days.Width=65;
        policy.Controls.Add(new Label{Text=L.T("背景錄影上限 GiB"),AutoSize=true,Margin=new Padding(0,8,4,0)});policy.Controls.Add(cap);policy.Controls.Add(new Label{Text=L.T("保留天數"),AutoSize=true,Margin=new Padding(5,8,4,0)});policy.Controls.Add(days);
        policy.Controls.Add(Button(L.T("預覽清理建議"),delegate{selected.Clear(); foreach(var r in Core.Plan(VisibleRows().ToList(),(double)cap.Value,(int)days.Value,DateTime.UtcNow))selected.Add(r.Path);RefreshGrid();}));
        policy.Controls.Add(Button(L.T("取消選取"),delegate{selected.Clear();RefreshGrid();}));
        policy.Controls.Add(Button(L.T("匯出報表"),delegate{using(var d=new SaveFileDialog{Filter=L.T("CSV 報表|*.csv"),FileName="SteamRecord-report.csv",OverwritePrompt=true})if(d.ShowDialog()==DialogResult.OK) SafeFiles.WriteReport(d.FileName,Core.Csv(rows),scannedRoot,true);}));
        layout.Controls.Add(policy,0,5);
        var indexOptions=new FlowLayoutPanel{Dock=DockStyle.Fill};syncIndex.Text=L.T("同步清理索引（測試版）");syncIndex.AutoSize=true;
        indexOptions.Controls.Add(syncIndex);indexOptions.Controls.Add(Button(L.T("修復失效索引…"),async delegate{await RepairIndex();}));
        indexOptions.Controls.Add(new Label{Text=L.T("保留時間軸、成就及剪輯中繼資料；寫入前備份。"),AutoSize=true,Margin=new Padding(8,8,0,0)});layout.Controls.Add(indexOptions,0,6);
        var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));
        status.Dock=DockStyle.Fill;bottom.Controls.Add(status);bottom.Controls.Add(Button(L.T("移至資源回收筒"),async delegate{await Clean(false);}));bottom.Controls.Add(Button(L.T("永久刪除…"),async delegate{await Clean(true);}));layout.Controls.Add(bottom,0,7);
        CaptureCaptions(this);
        language.SelectedIndexChanged+=delegate{ChangeLanguage();};
        ApplyLanguage();
        Shown+=async delegate{if(autoScan && root.Text!="")await Scan();};
        FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;MessageBox.Show(L.T("工作進行中，請等候完成。"));}};
    }
    void CaptureCaptions(Control c){if((c is Form || c is Label || c is Button || c is CheckBox) && c!=summary && c!=status && c.Text!="")captions[c]=L.Canonical(c.Text);foreach(Control child in c.Controls)CaptureCaptions(child);}
    void ApplyLanguage(){SuspendLayout();try{foreach(var pair in captions)pair.Key.Text=L.T(pair.Value);foreach(DataGridViewColumn col in grid.Columns)col.HeaderText=L.T(col.Index==0?"選取":col.Name);RefreshGrid();if(scannedRoot=="")summary.Text=L.T("選擇 Steam 錄影資料夾後掃描；不會自動清理。");}finally{ResumeLayout(true);}}
    void ChangeLanguage(){if(changingLanguage || language.SelectedIndex<0)return;changingLanguage=true;int previous=L.Index;try{L.Index=language.SelectedIndex;try{L.Save(languageFile);}catch(Exception ex){L.Index=previous;language.SelectedIndex=previous;MessageBox.Show(this,L.T("無法儲存語言設定：")+ex.Message);return;}ApplyLanguage();}finally{changingLanguage=false;}}
#if TESTS
    public static void TestLanguages(string testDirectory){
        L.TestCatalog();string preferences=System.IO.Path.Combine(testDirectory,"language-tests");
        using(var form=new MainForm(preferences,false)){
            var item=new Recording{Path="language-test-row",Name="bg_123_20200101_120000",App="123",Game="Fixture game",Kind="背景錄影",Error="",Bytes=123,Modified=DateTime.UtcNow.AddDays(-60)};
            form.rows.Add(item);form.scannedRoot=testDirectory;form.selected.Add(item.Path);form.cap.Value=321;form.days.Value=45;form.search.Text="Fixture";form.RefreshGrid();
            foreach(int index in new[]{2,1,0,2}){
                form.language.SelectedIndex=index;
                if(L.Index!=index || form.grid.Columns[1].HeaderText!=L.T("遊戲") || form.grid.Rows[0].Cells[2].Value.ToString()!=L.T("背景錄影"))throw new Exception("Language did not update grid.");
                if(!form.actions.Any(b=>b.Text==L.T("預覽清理建議")) || !form.Text.EndsWith("1.4.0-preview.1"))throw new Exception("Language did not update controls.");
                if(!form.selected.Contains(item.Path) || !Convert.ToBoolean(form.grid.Rows[0].Cells[0].Value) || form.search.Text!="Fixture" || form.cap.Value!=321 || form.days.Value!=45 || item.Kind!="背景錄影")throw new Exception("Language switch changed user state.");
                if(Core.Plan(form.rows,0,30,DateTime.UtcNow).Count!=1)throw new Exception("Language switch changed cleanup rules.");
                if(L.Error("未知資料夾命名：test-folder")!=L.T("未知資料夾命名：")+"test-folder")throw new Exception("Error localization failed.");
                foreach(var c in form.captions)if(c.Key.Text!=L.T(c.Value))throw new Exception("Stale caption.");
            }
        }
        using(var reopened=new MainForm(preferences,false)){if(reopened.language.SelectedIndex!=2 || reopened.grid.Columns[1].HeaderText!="Game")throw new Exception("Language preference did not persist.");}
        File.WriteAllText(System.IO.Path.Combine(preferences,"language.txt"),"invalid-code");L.Load(System.IO.Path.Combine(preferences,"language.txt"));if(L.Index!=0)throw new Exception("Invalid language fallback failed.");
    }
#endif
    Button Button(string text,EventHandler click) { var b=new Button{Text=text,AutoSize=true,Height=33,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(43,65,85),ForeColor=Color.White,Margin=new Padding(4)};b.Click+=delegate(object s,EventArgs e){try{click(s,e);}catch(Exception ex){MessageBox.Show(this,L.Error(ex.Message),L.T("無法完成"));}}; actions.Add(b);return b; }
    void Busy(bool value){busy=value;foreach(var c in actions)c.Enabled=!value;root.Enabled=!value;grid.Enabled=!value;search.Enabled=!value;clips.Enabled=!value;cap.Enabled=!value;days.Enabled=!value;language.Enabled=!value;syncIndex.Enabled=!value;}
    async Task Scan(){ if(busy)return;Busy(true);status.Text=L.T("正在掃描完整工作階段…");try {string p=Core.Full(root.Text);var found=await Task.Run(()=>Core.Scan(p));rows=found;scannedRoot=p;selected.Clear();foreach(var r in rows)r.Pinned=pins.Contains(r.Path);RefreshGrid();}catch(Exception ex){MessageBox.Show(this,L.Error(ex.Message),L.T("掃描失敗"));status.Text=L.T("掃描未完成。");}finally{Busy(false);} }
    IEnumerable<Recording> VisibleRows(){string q=search.Text.Trim();return rows.Where(r=>(clips.Checked || r.Kind=="背景錄影" || r.Kind=="略過") && (q=="" || ((r.Game??"")+" "+r.App+" "+r.Name).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)).OrderByDescending(r=>r.Bytes);}
    void RefreshGrid(){ if(busy && scannedRoot=="")return;loading=true;grid.Rows.Clear();foreach(var r in VisibleRows()){int i=grid.Rows.Add(selected.Contains(r.Path),L.T(r.Game),L.T(r.Kind),(r.Bytes/Core.GiB).ToString("N2"),r.Modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),r.Pinned?L.T("已保護"):r.Error==""?L.T("可管理"):L.Error(r.Error),r.Name);grid.Rows[i].Tag=r;grid.Rows[i].Cells[0].ReadOnly=r.Pinned || !String.IsNullOrEmpty(r.Error);}loading=false;summary.Text=String.Format(L.T("已辨識 {0:N2} GiB　背景 {1:N2} GiB　手動 {2:N2} GiB　剪輯 {3:N2} GiB　／ {4} 個工作階段，略過 {5} 項"),rows.Sum(r=>r.Bytes)/Core.GiB,rows.Where(r=>r.Kind=="背景錄影").Sum(r=>r.Bytes)/Core.GiB,rows.Where(r=>r.Kind=="手動錄影").Sum(r=>r.Bytes)/Core.GiB,rows.Where(r=>r.Kind=="剪輯").Sum(r=>r.Bytes)/Core.GiB,rows.Count(r=>r.Kind!="略過"),rows.Count(r=>r.Kind=="略過"));UpdateStatus();}
    void UpdateStatus(){status.Text=String.Format(L.T("已選 {0} 項 / {1:N2} GiB\n建議僅作用於搜尋範圍；0 表示停用該規則。"),selected.Count,rows.Where(r=>selected.Contains(r.Path)).Sum(r=>r.Bytes)/Core.GiB);}
    Recording Current(){return grid.CurrentRow==null?null:grid.CurrentRow.Tag as Recording;}
    void TogglePin(){var r=Current();if(r==null)return;r.Pinned=!r.Pinned;if(r.Pinned){pins.Add(r.Path);selected.Remove(r.Path);}else pins.Remove(r.Path);try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(settings));SafeFiles.CheckFile(settings);File.WriteAllLines(settings,pins);}catch{r.Pinned=!r.Pinned;if(r.Pinned)pins.Add(r.Path);else pins.Remove(r.Path);RefreshGrid();throw;}RefreshGrid();}
    bool Confirm(List<Recording> targets,bool permanent){using(var f=new Form{Text=L.T("確認清理預覽"),Width=720,Height=530,StartPosition=FormStartPosition.CenterParent,Font=Font}){
        string indexNote=syncIndex.Checked?L.T("將同步移除已刪錄影的索引參照；保留時間軸。索引寫入前會備份，尚未經你的 Steam 重新載入驗證。"):L.T("Steam 可能保留舊的清單項目；本工具不修改 gamerecording.pb 與 timelines。");
        var t=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Top,Height=360,Text=String.Format(L.T("目錄：{0}\r\n{1} 個完整工作階段，{2:N2} GiB\r\n\r\n{3}\r\n\r\n{4}\r\n\r\n"),scannedRoot,targets.Count,targets.Sum(r=>r.Bytes)/Core.GiB,permanent?L.T("永久刪除無法復原。備份只含索引，不含錄影。"):L.T("移至資源回收筒可復原。清空回收筒後才釋放同一磁碟的空間。若系統回收筒不可用，將回報失敗。"),indexNote)+String.Join("\r\n",targets.Select(r=>r.Game+" | "+r.Name+" | "+(r.Bytes/Core.GiB).ToString("N2")+" GiB"))};
        var entry=new TextBox{Dock=DockStyle.Top};var hint=new Label{Text=permanent?L.T("確認清單後輸入 DELETE，再按確認。"):L.T("確認清單後輸入 RECYCLE，再按確認。"),Dock=DockStyle.Top,Height=32};
        var ok=new Button{Text=L.T("確認"),Dock=DockStyle.Bottom,Height=38};ok.Click+=delegate{if(entry.Text==(permanent?"DELETE":"RECYCLE")){f.DialogResult=DialogResult.OK;f.Close();}};f.Controls.Add(ok);f.Controls.Add(entry);f.Controls.Add(hint);f.Controls.Add(t);return f.ShowDialog(this)==DialogResult.OK;
    }}
    async Task Clean(bool permanent){ if(busy)return;var targets=rows.Where(r=>selected.Contains(r.Path)).ToList();if(targets.Count==0)return;
        if(Core.SteamRunning()){MessageBox.Show(this,L.T("請先從 Steam 選單完全離開 Steam，再執行清理。掃描可在 Steam 開啟時使用。"));return;}
        try{if(!String.Equals(Core.Full(root.Text),scannedRoot,StringComparison.OrdinalIgnoreCase)){MessageBox.Show(L.T("路徑已變更，請重新掃描。"));return;}}catch(Exception ex){MessageBox.Show(L.Error(ex.Message));return;}
        if(!Confirm(targets,permanent))return;bool synchronize=syncIndex.Checked;Busy(true);status.Text=L.T("正在驗證並清理…");
        try {string result=await Task.Run(()=>RunCleanup(targets,permanent,synchronize));MessageBox.Show(this,result,L.T("清理結果"));}catch(Exception ex){MessageBox.Show(this,L.Error(ex.Message),L.T("已停止清理"));}finally{Busy(false);}await Scan();
    }
    string RunCleanup(List<Recording> targets,bool permanent,bool synchronize){
        if(Core.SteamRunning())throw new IOException(L.T("Steam 已重新啟動，取消清理。"));
        foreach(var r in targets)Core.Validate(scannedRoot,r);
        byte[] expectedIndex=synchronize?RecordingIndex.Preview(scannedRoot,targets.Select(r=>r.Name),false).Before:null;
        string batch=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settings),"history",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(batch);
        Core.NoLink(batch);
        // Abort before changes if any metadata backup or index preflight fails.
        string pb=System.IO.Path.Combine(scannedRoot,"gamerecording.pb");if(File.Exists(pb)){SafeFiles.CheckFile(pb);File.Copy(pb,System.IO.Path.Combine(batch,"gamerecording.pb"));}
        string timelines=System.IO.Path.Combine(scannedRoot,"timelines");if(Directory.Exists(timelines))BackupTree(timelines,System.IO.Path.Combine(batch,"timelines"));
        File.WriteAllText(System.IO.Path.Combine(batch,"plan.csv"),Core.Csv(targets),new UTF8Encoding(true));
        int done=0;long bytes=0;string log=System.IO.Path.Combine(batch,"result.txt");
        foreach(var r in targets){
            try {
                if(Core.SteamRunning())throw new IOException(L.T("Steam 已啟動。"));Core.Validate(scannedRoot,r);
                IndexPlan step=null;
                if(synchronize){if(!RecordingIndex.Read(pb).SequenceEqual(expectedIndex))throw new IOException(L.T("索引已變動，請重新掃描；尚未套用索引修改。"));step=RecordingIndex.Edit(expectedIndex,new[]{r.Name});}
                File.AppendAllText(log,"START "+r.Path+Environment.NewLine);
                Core.Remove(scannedRoot,r,permanent);
                if(synchronize){RecordingIndex.Commit(scannedRoot,step,batch);expectedIndex=step.After;File.AppendAllText(log,"INDEX "+step.Description+Environment.NewLine);}
                File.AppendAllText(log,"OK "+r.Path+Environment.NewLine);done++;bytes+=r.Bytes;
            }catch(Exception ex){File.AppendAllText(log,"FAILED "+r.Path+" "+ex.Message+Environment.NewLine);return L.T("已完成 ")+done+L.T(" 項，其餘停止。\n")+L.Error(ex.Message)+L.T("\n紀錄：")+batch;}
        }
        return String.Format(L.T("完成 {0} 項，共 {1:N2} GiB。{2}\n索引備份與操作紀錄：{3}"),done,bytes/Core.GiB,permanent?L.T("錄影已永久刪除。"):L.T("請確認內容後自行清空資源回收筒以釋放磁碟空間。"),batch);
    }
    async Task RepairIndex(){
        if(busy)return;
        if(Core.SteamRunning()){MessageBox.Show(this,L.T("請先從 Steam 選單完全離開 Steam，再執行清理。掃描可在 Steam 開啟時使用。"));return;}
        try{if(scannedRoot=="" || !String.Equals(Core.Full(root.Text),scannedRoot,StringComparison.OrdinalIgnoreCase)){MessageBox.Show(L.T("路徑已變更，請重新掃描。"));return;}}catch(Exception ex){MessageBox.Show(ex.Message);return;}
        Busy(true);try{
            var plan=await Task.Run(()=>RecordingIndex.Preview(scannedRoot,new string[0],true));
            if(!plan.Changed){MessageBox.Show(this,L.T("沒有可確認失效的本機錄影或剪輯參照；未修改索引。"));return;}
            using(var dialog=new Form{Text=L.T("修復失效索引…"),Width=820,Height=560,Font=Font,StartPosition=FormStartPosition.CenterParent}){
                var text=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Top,Height=370,Text=scannedRoot+"\r\n\r\n"+plan.Description+"\r\n\r\n"+L.T("只清除來源資料夾確實不存在的本機參照，不會刪除影片。保留所有時間軸、成就與雲端參照。這是副本測試通過、尚未經 Steam 實機載入驗證的功能。")+"\r\n\r\n"+String.Join("\r\n",plan.Removed.Distinct())};
                var hint=new Label{Text=L.T("確認清單後輸入 REPAIR，再按確認。"),Dock=DockStyle.Top,Height=35};var entry=new TextBox{Dock=DockStyle.Top};var ok=new Button{Text=L.T("確認"),Dock=DockStyle.Bottom,Height=38};ok.Click+=delegate{if(entry.Text=="REPAIR")dialog.DialogResult=DialogResult.OK;};dialog.Controls.Add(ok);dialog.Controls.Add(entry);dialog.Controls.Add(hint);dialog.Controls.Add(text);
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            }
            string batch=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settings),"history","index-repair-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            await Task.Run(()=>{var current=RecordingIndex.Preview(scannedRoot,new string[0],true);if(!current.Before.SequenceEqual(plan.Before) || !current.After.SequenceEqual(plan.After))throw new IOException(L.T("索引或資料夾已變動，請重新預覽。"));RecordingIndex.Commit(scannedRoot,plan,batch);File.WriteAllText(System.IO.Path.Combine(batch,"result.txt"),plan.Description+Environment.NewLine+String.Join(Environment.NewLine,plan.Removed));});
            MessageBox.Show(this,plan.Description+"\n"+L.T("索引備份與操作紀錄：")+batch,L.T("清理結果"));
        }catch(Exception ex){MessageBox.Show(this,L.Error(ex.Message),L.T("已停止清理"));}finally{Busy(false);}
    }
    static void BackupTree(string src,string dst){Core.NoLink(src);Directory.CreateDirectory(dst);foreach(var f in new DirectoryInfo(src).GetFiles()){if((f.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException(L.T("索引包含連結。"));File.Copy(f.FullName,System.IO.Path.Combine(dst,f.Name));}foreach(string d in Directory.GetDirectories(src))BackupTree(d,System.IO.Path.Combine(dst,System.IO.Path.GetFileName(d)));}
}
static class Program {
    [STAThread] public static int Main(string[] args){
        try {
            if(args.Length==3 && args[0]=="--scan"){var rows=Core.Scan(args[1]);SafeFiles.WriteReport(args[2],Core.Csv(rows),args[1]);return 0;}


            if(args.Length==3 && args[0]=="--index-audit"){RecordingIndex.Audit(args[1],args[2]);return 0;}
            if(args.Length>0){Console.Error.WriteLine("Usage: SteamRecordManager.exe [--scan ROOT NEW_REPORT.csv | --index-audit ROOT NEW_REPORT.txt]");return 2;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());return 0;
        }catch(Exception ex){if(args.Length>0){Console.Error.WriteLine(ex);return 1;}MessageBox.Show(ex.ToString(),L.T("Steam Record 空間管理員"));return 1;}
    }
}
}
