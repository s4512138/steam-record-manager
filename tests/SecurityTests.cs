using System;
using System.IO;
using System.Linq;
using System.Reflection;
namespace SteamRecordManager {
public static class SecurityTests {
    static void Assert(bool value,string text){if(!value)throw new Exception("Security regression: "+text);}
    static bool Refuses(Action action){try{action();return false;}catch(IOException){return true;}catch(InvalidDataException){return true;}}
    public static void Run(string root,string[] args){
        foreach(string input in new[]{"=1+1","+SUM(A1)","-1+1","@SUM(A1)","  =1+1","\t=1+1","\r=1+1","\n=1+1"})Assert(Core.CsvCell(input).StartsWith("\"'"),"CSV formula-like cell must be text");
        Assert(Core.CsvCell("Normal game")=="\"Normal game\"" && Core.CsvCell("a\"b")=="\"a\"\"b\"","normal CSV quoting");
        string recordings=Path.Combine(root,"reports-fixture");Directory.CreateDirectory(Path.Combine(recordings,"video"));Directory.CreateDirectory(Path.Combine(recordings,"clips"));
        string inside=Path.Combine(recordings,"video","report.csv");Assert(Refuses(()=>SafeFiles.WriteReport(inside,"x",recordings)),"report in recording tree refused");Assert(!File.Exists(inside),"source untouched");
        Assert(Refuses(()=>SafeFiles.WriteReport(Path.Combine(root,"no-source.csv"),"x","")),"scan required for report export");
        Assert(SafeFiles.IsWithin(inside,recordings.ToUpperInvariant()) && !SafeFiles.IsWithin(recordings+"-other\\report.csv",recordings),"case-insensitive path boundary");
        string report=Path.Combine(root,"report.csv");SafeFiles.WriteReport(report,"first",recordings);Assert(Refuses(()=>SafeFiles.WriteReport(report,"second",recordings)),"CLI cannot overwrite");Assert(File.ReadAllText(report)=="first","existing report retained");SafeFiles.WriteReport(report,"confirmed",recordings,true);Assert(File.ReadAllText(report)=="confirmed","confirmed GUI replacement");
        byte[] tooMany=new byte[200002];for(int i=0;i<tooMany.Length;i+=2)tooMany[i]=8;Assert(Refuses(()=>RecordingIndex.Parse(tooMany)),"protobuf field-count bound");
        Assert(Refuses(()=>RecordingIndex.Parse(new byte[]{8,255,255,255,255,255,255,255,255,255,2})),"varint overflow");
        if(args.Length!=2)throw new Exception("Use scripts/test.ps1 to include the required junction and release checks.");
        string link=args[0];Assert(Refuses(()=>Core.NoLink(link)),"junction rejected");Assert(Refuses(()=>Core.Inspect(link,new System.Collections.Generic.Dictionary<string,string>())),"linked session rejected");
        Assert(Refuses(()=>SafeFiles.WriteReport(Path.Combine(link,"new-report.csv"),"unsafe",recordings)),"export through junction rejected");Assert(File.ReadAllText(Path.Combine(link,"sentinel.txt"))=="do not modify" && !File.Exists(Path.Combine(link,"new-report.csv")),"outside target retained");
        var release=Assembly.LoadFile(Path.GetFullPath(args[1]));Assert(release.GetType("SteamRecordManager.Tests")==null && release.GetType("SteamRecordManager.IndexTests")==null,"test suites absent from release");
        Assert(release.GetType("SteamRecordManager.Core").GetMethod("RemoveForTest")==null && release.GetType("SteamRecordManager.RecordingIndex").GetMethod("CommitForTest")==null,"Steam bypass hooks absent from release");
    }
}
}
