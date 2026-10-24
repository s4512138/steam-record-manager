using System;
using System.IO;
using System.Text;

namespace SteamRecordManager {
public static class SafeFiles {
    public static bool IsWithin(string candidate,string root){candidate=Core.Full(candidate);root=Core.Full(root);return candidate.Equals(root,StringComparison.OrdinalIgnoreCase) || candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    public static void CheckFile(string path){path=Core.Full(path);if(path.StartsWith(@"\\",StringComparison.Ordinal))throw new IOException(L.T("只支援本機磁碟的錄影資料夾。"));Core.NoLink(Path.GetDirectoryName(path));if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException(L.T("包含連結檔案。"));}
    // CLI exports never overwrite a file. GUI replacement requires the native Save dialog confirmation.
    public static void WriteReport(string path,string contents,string recordingRoot,bool confirmedOverwrite=false){
        if(String.IsNullOrWhiteSpace(recordingRoot))throw new IOException(L.T("請先完成掃描再匯出報表。"));
        path=Core.Full(path);if(IsWithin(path,recordingRoot))throw new IOException(L.T("報表不可儲存在錄影根目錄內。"));CheckFile(path);
        bool replace=confirmedOverwrite && File.Exists(path);
        string staging=Path.Combine(Path.GetDirectoryName(path),".srm-report-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using(var stream=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(stream,new UTF8Encoding(true))){writer.Write(contents);writer.Flush();stream.Flush(true);}
            CheckFile(path);if(replace)File.Replace(staging,path,null);else File.Move(staging,path);
        }finally{if(File.Exists(staging))File.Delete(staging);}
    }
}
}
