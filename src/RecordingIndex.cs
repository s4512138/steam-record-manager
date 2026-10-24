using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace SteamRecordManager {
// Lossless protobuf editing. Unknown fields, field order and untouched messages
// retain their original bytes. We never remove timelines, phases or timeline JSON.
public class WireField {
    public int Number,Type; public byte[] Raw,Data;public ulong Value;
}
public class IndexPlan {
    public byte[] Before,After;public List<string> Removed=new List<string>();
    public int RecordingRefs,ClipRefs,Timelines;
    public bool Changed {get{return !Before.SequenceEqual(After);}}
    public string Description {get{return String.Format(L.T("移除 {0} 個錄影參照、{1} 個剪輯參照；保留全部 {2} 條時間軸。"),RecordingRefs,ClipRefs,Timelines);}}
}
public static class RecordingIndex {
    static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    static Exception Bad(){return new InvalidDataException(L.T("索引格式無法確認，已停止；未猜測修改。"));}
    static ulong Varint(byte[] b,ref int p){ulong v=0;for(int i=0;i<10;i++){if(p>=b.Length)throw Bad();byte n=b[p++];if(i==9 && n>1)throw Bad();v|=(ulong)(n&127)<<(7*i);if((n&128)==0)return v;}throw Bad();}
    static void WriteVarint(Stream stream,ulong v){while(v>=128){stream.WriteByte((byte)((v&127)|128));v>>=7;}stream.WriteByte((byte)v);}
    public static List<WireField> Parse(byte[] bytes){
        if(bytes.Length>67108864)throw Bad();var fields=new List<WireField>();int p=0;
        while(p<bytes.Length){int start=p;ulong tag=Varint(bytes,ref p);if(tag<8 || tag>UInt32.MaxValue)throw Bad();int wire=(int)(tag&7);var f=new WireField{Number=(int)(tag>>3),Type=wire};
            if(wire==0)f.Value=Varint(bytes,ref p);
            else {ulong n;if(wire==2)n=Varint(bytes,ref p);else if(wire==1)n=8;else if(wire==5)n=4;else throw Bad();if(n>(ulong)(bytes.Length-p))throw Bad();f.Data=new byte[(int)n];Buffer.BlockCopy(bytes,p,f.Data,0,(int)n);p+=(int)n;}
            f.Raw=new byte[p-start];Buffer.BlockCopy(bytes,start,f.Raw,0,p-start);fields.Add(f);if(fields.Count>100000)throw Bad();
        }return fields;
    }
    public static byte[] Join(IEnumerable<WireField> fields){using(var stream=new MemoryStream()){foreach(var f in fields)stream.Write(f.Raw,0,f.Raw.Length);return stream.ToArray();}}
    public static WireField Message(int number,byte[] data){using(var stream=new MemoryStream()){WriteVarint(stream,(ulong)((number<<3)|2));WriteVarint(stream,(ulong)data.Length);stream.Write(data,0,data.Length);return new WireField{Number=number,Type=2,Data=data,Raw=stream.ToArray()};}}
    static string String(List<WireField> fields,int n,bool required=false){var matches=fields.Where(f=>f.Number==n).ToList();if(matches.Count>1 || matches.Any(f=>f.Type!=2) || (required && matches.Count!=1))throw Bad();if(matches.Count==0)return "";try{return Utf8.GetString(matches[0].Data);}catch{throw Bad();}}
    static List<WireField> Children(WireField f){if(f.Type!=2)throw Bad();return Parse(f.Data);}
    static bool LocalRecording(string id){return System.Text.RegularExpressions.Regex.IsMatch(id,@"^(bg|fg)_\d+_\d{8}_\d{6}$");}
    static bool LocalClip(string id){return System.Text.RegularExpressions.Regex.IsMatch(id,@"^clip_\d+_\d{8}_\d{6}$");}
    public static IndexPlan Edit(byte[] before,IEnumerable<string> deleted,HashSet<string> existingVideo=null,HashSet<string> existingClips=null){
        var plan=new IndexPlan{Before=before};var names=new HashSet<string>(deleted,StringComparer.Ordinal);var output=new List<WireField>();var original=Parse(before);
        // Missing tag references alone do not prove a clip is local: only the
        // explicitly selected clips or the index's temporary_clips list qualify.
        var temporary=new HashSet<string>(original.Where(f=>f.Number==3 && f.Type==2).Select(f=>Utf8.GetString(f.Data)),StringComparer.Ordinal);
        Func<string,bool> removeClip=id=>LocalClip(id) && (names.Contains(id) || (existingClips!=null && temporary.Contains(id) && !existingClips.Contains(id)));
        foreach(var field in original){
            if(field.Number==1){
                var timeline=Children(field);String(timeline,1,true);plan.Timelines++;bool changed=false;var kept=new List<WireField>();
                foreach(var entry in timeline){
                    if(entry.Number!=5){kept.Add(entry);continue;}
                    var recording=Children(entry);string id=String(recording,1,true);
                    // Remote recordings may legitimately have no local video directory.
                    bool remote=recording.Any(f=>f.Number==6 || f.Number==7 || f.Number==8);
                    bool explicitRemoval=names.Contains(id);
                    if(explicitRemoval && remote)throw new IOException(L.T("錄影含雲端參照，無法自動同步索引：")+id);
                    bool missing=existingVideo!=null && LocalRecording(id) && !existingVideo.Contains(id) && !remote;
                    if(LocalRecording(id) && (explicitRemoval || missing)){changed=true;plan.RecordingRefs++;plan.Removed.Add(id);}
                    else kept.Add(entry);
                }
                output.Add(changed?Message(1,Join(kept)):field);
            }else if(field.Number==3){
                if(field.Type!=2)throw Bad();string id=Utf8.GetString(field.Data);
                if(removeClip(id)){plan.ClipRefs++;plan.Removed.Add(id);}else output.Add(field);
            }else if(field.Number==4){
                var tag=Children(field);var kept=new List<WireField>();bool changed=false;
                foreach(var entry in tag){
                    if(entry.Number!=3){kept.Add(entry);continue;}
                    var reference=Children(entry);string clip=String(reference,1);
                    if(removeClip(clip)){changed=true;plan.ClipRefs++;plan.Removed.Add(clip);}else kept.Add(entry);
                }
                output.Add(changed?Message(4,Join(kept)):field);
            }else output.Add(field);
        }
        plan.After=Join(output);Parse(plan.After);return plan;
    }
    public static byte[] Read(string path){Core.NoLink(System.IO.Path.GetDirectoryName(path));if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw Bad();using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){if(stream.Length>67108864)throw Bad();using(var reader=new BinaryReader(stream)){byte[] bytes=reader.ReadBytes((int)stream.Length);if(bytes.Length!=stream.Length)throw Bad();return bytes;}}}
    public static IndexPlan Preview(string root,IEnumerable<string> deleted,bool repair){
        Core.RootCheck(root);var before=Read(System.IO.Path.Combine(root,"gamerecording.pb"));HashSet<string> videos=null,clips=null;
        if(repair){string video=System.IO.Path.Combine(root,"video"),clip=System.IO.Path.Combine(root,"clips");Core.NoLink(video);Core.NoLink(clip);videos=new HashSet<string>(Directory.GetDirectories(video).Select(System.IO.Path.GetFileName),StringComparer.Ordinal);clips=new HashSet<string>(Directory.GetDirectories(clip).Select(System.IO.Path.GetFileName),StringComparer.Ordinal);}
        return Edit(before,deleted,videos,clips);
    }
    public static void Commit(string root,IndexPlan plan,string history){CommitInternal(root,plan,history,Core.SteamRunning);}
#if TESTS
    public static void CommitForTest(string root,IndexPlan plan,string history,Func<bool> steamRunning){CommitInternal(root,plan,history,steamRunning);}
#endif
    static void CommitInternal(string root,IndexPlan plan,string history,Func<bool> steamRunning){
        if(steamRunning())throw new IOException(L.T("Steam 已啟動。"));
        Core.RootCheck(root);string path=System.IO.Path.Combine(root,"gamerecording.pb");
        if(!Read(path).SequenceEqual(plan.Before))throw new IOException(L.T("索引已變動，請重新掃描；尚未套用索引修改。"));
        if(!plan.Changed)return;
        Directory.CreateDirectory(history);Core.NoLink(history);string backup=System.IO.Path.Combine(history,"index-before-"+Guid.NewGuid().ToString("N")+".pb");using(var backupFile=new FileStream(backup,FileMode.CreateNew,FileAccess.Write,FileShare.None)){backupFile.Write(plan.Before,0,plan.Before.Length);backupFile.Flush(true);}
        string temp=System.IO.Path.Combine(root,".srm-index-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(plan.After,0,plan.After.Length);stream.Flush(true);}
            if(!Read(temp).SequenceEqual(plan.After))throw Bad();
            // Hold a read handle denying writes. FileShare.Delete permits ReplaceFile's atomic replacement.
            using(var guard=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
                using(var reader=new BinaryReader(guard,Encoding.UTF8,true)){if(guard.Length!=plan.Before.Length || !reader.ReadBytes(plan.Before.Length).SequenceEqual(plan.Before))throw new IOException(L.T("索引已變動，請重新掃描；尚未套用索引修改。"));}
                if(steamRunning())throw new IOException(L.T("Steam 已啟動。"));File.Replace(temp,path,null);
            }
            if(!Read(path).SequenceEqual(plan.After))throw new IOException(L.T("索引寫入後驗證失敗，請查看備份與操作紀錄。"));
        }finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static void Audit(string root,string report){
        var before=Read(System.IO.Path.Combine(root,"gamerecording.pb"));var noop=Edit(before,new string[0]);if(!before.SequenceEqual(noop.After))throw new Exception("No-op changed index bytes");
        var names=Directory.GetDirectories(System.IO.Path.Combine(root,"video")).Select(System.IO.Path.GetFileName).ToList();var all=Edit(before,names);var repair=Preview(root,new string[0],true);
        bool retained=Parse(before).Where(f=>f.Number!=1).All(f=>Parse(all.After).Any(a=>a.Raw.SequenceEqual(f.Raw)));
        SafeFiles.WriteReport(report,"Read-only audit. No Steam data changed.\r\nTimelines: "+noop.Timelines+"\r\nRemoving all local video folders would remove "+all.RecordingRefs+" recording references, preserving all timelines.\r\nCurrently missing recording references: "+repair.RecordingRefs+"\r\nMissing clip references: "+repair.ClipRefs+"\r\nNo-op byte-for-byte preservation: PASS\r\nUntouched root fields: "+retained,root);
    }
}
}
