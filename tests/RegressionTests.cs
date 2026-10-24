using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
namespace SteamRecordManager {
public static class Tests {
    static void Assert(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);}
    static void WriteCString(BinaryWriter writer,string value){writer.Write(Encoding.UTF8.GetBytes(value));writer.Write((byte)0);}
    static void WriteKey(BinaryWriter writer,string key,int version){if(version==41)writer.Write(Array.IndexOf(new[]{"appinfo","unrelated","name","common"},key));else WriteCString(writer,key);}
    static void CacheFixture(string filename,int version){
        using(var stream=File.Create(filename))using(var writer=new BinaryWriter(stream)){
            writer.Write((uint)(0x07564400|version));writer.Write((uint)1);if(version==41)writer.Write((long)0);
            // Broken entry must be skipped without affecting the next entry.
            writer.Write((uint)999);writer.Write((uint)((version>=40?60:40)+1));writer.Write(new byte[version>=40?60:40]);writer.Write((byte)255);
            writer.Write((uint)123);long sizePos=stream.Position;writer.Write((uint)0);long begin=stream.Position;writer.Write(new byte[version>=40?60:40]);
            writer.Write((byte)0);WriteKey(writer,"appinfo",version);
            writer.Write((byte)0);WriteKey(writer,"unrelated",version);writer.Write((byte)1);WriteKey(writer,"name",version);WriteCString(writer,"Wrong name");writer.Write((byte)8);
            writer.Write((byte)0);WriteKey(writer,"common",version);writer.Write((byte)1);WriteKey(writer,"name",version);WriteCString(writer,"測試遊戲 ✓");writer.Write((byte)8);writer.Write((byte)8);writer.Write((byte)8);
            long end=stream.Position;stream.Position=sizePos;writer.Write((uint)(end-begin));stream.Position=end;writer.Write((uint)0);
            if(version==41){long table=stream.Position;writer.Write((uint)4);foreach(var key in new[]{"appinfo","unrelated","name","common"})WriteCString(writer,key);stream.Position=8;writer.Write(table);}
        }
    }
    public static int Run(string baseDir){
        string root=System.IO.Path.Combine(Core.Full(baseDir),"fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(System.IO.Path.Combine(root,"video"));Directory.CreateDirectory(System.IO.Path.Combine(root,"clips"));
        string p=System.IO.Path.Combine(root,"video","bg_123_20200101_120000");Directory.CreateDirectory(p);File.WriteAllText(System.IO.Path.Combine(p,"session.mpd"),"test");File.SetLastWriteTimeUtc(System.IO.Path.Combine(p,"session.mpd"),DateTime.UtcNow.AddDays(-60));Directory.SetLastWriteTimeUtc(p,DateTime.UtcNow.AddDays(-60));
        var rows=Core.Scan(root);Assert(rows.Count==1 && rows[0].Bytes==4,"scan session size");Core.Validate(root,rows[0]);
        Assert(Core.Plan(rows,0,30,DateTime.UtcNow).Count==1,"age");rows[0].Pinned=true;Assert(Core.Plan(rows,0,30,DateTime.UtcNow).Count==0,"protect");rows[0].Pinned=false;
        Assert(Core.Plan(rows,0,0,DateTime.UtcNow).Count==0,"zero disables rules");
        File.AppendAllText(System.IO.Path.Combine(p,"session.mpd"),"changed");bool refused=false;try{Core.Validate(root,rows[0]);}catch(IOException){refused=true;}Assert(refused,"changed file rejected");
        rows=Core.Scan(root);Assert(Core.Plan(rows,0,30,DateTime.UtcNow).Count==0,"recent write rejected");
        refused=false;try{Core.Validate(System.IO.Path.GetDirectoryName(root),rows[0]);}catch(IOException){refused=true;}Assert(refused,"outside root rejected");
        Directory.CreateDirectory(System.IO.Path.Combine(root,"video","unrecognized"));Assert(Core.Scan(root).Any(r=>r.Kind=="略過"),"unknown skipped");
        var synthetic=new List<Recording>{new Recording{Path="a",Kind="背景錄影",Bytes=(long)Core.GiB*60,Modified=DateTime.UtcNow.AddDays(-60)},new Recording{Path="b",Kind="背景錄影",Bytes=(long)Core.GiB*60,Modified=DateTime.UtcNow.AddDays(-20)},new Recording{Path="c",Kind="剪輯",Bytes=(long)Core.GiB*10,Modified=DateTime.UtcNow.AddDays(-80)}};
        var plan=Core.Plan(synthetic,100,0,DateTime.UtcNow);Assert(plan.Count==1 && plan[0].Path=="a","quota oldest first and clips excluded");
        synthetic[0].Pinned=true;plan=Core.Plan(synthetic,1,0,DateTime.UtcNow);Assert(plan.Count==1 && plan[0].Path=="b","protected quota");
        Assert(Core.Csv(rows).StartsWith("Game,AppID"),"report");
        File.SetLastWriteTimeUtc(System.IO.Path.Combine(p,"session.mpd"),DateTime.UtcNow.AddDays(-60));Directory.SetLastWriteTimeUtc(p,DateTime.UtcNow.AddDays(-60));
        var deletion=Core.Scan(root).First(r=>r.Kind=="背景錄影");Core.RemoveForTest(root,deletion,true);Assert(!Directory.Exists(p) && Directory.Exists(System.IO.Path.Combine(root,"clips")),"delete fixture only");
        foreach(int version in new[]{39,40,41}){
            string cache=System.IO.Path.Combine(root,"appinfo-"+version+".vdf");CacheFixture(cache,version);var names=AppInfoNames.Read(cache);
            Assert(names.Count==1 && names["123"]=="測試遊戲 ✓","appinfo v"+version+" unicode, exact common/name path, broken entry isolation");
        }
        string invalid=System.IO.Path.Combine(root,"invalid.vdf");File.WriteAllBytes(invalid,new byte[]{1,2,3,4,0,0,0,0});bool rejected=false;try{AppInfoNames.Read(invalid);}catch(InvalidDataException){rejected=true;}Assert(rejected,"unsupported cache fails safely");
        MainForm.TestLanguages(root);
        File.WriteAllText(System.IO.Path.Combine(baseDir,"test-result.txt"),"PASS: scan, age, protect, disabled rules, change detection, recent writes, path boundary, unknown folders, quota order, protected quota, CSV, deletion of generated fixture only; appinfo v39/v40/v41 Unicode names, exact common/name matching, broken entry isolation, invalid cache rejection; three language catalog and format placeholders, live control and row translations, selection/search/policy preservation, unchanged cleanup behavior, localized errors, preference persistence, invalid preference fallback.\r\nFixture retained: "+root);
        return 0;
    }
}
public static class IndexTests {
    static WireField Text(int n,string s){return RecordingIndex.Message(n,Encoding.UTF8.GetBytes(s));}
    static byte[] Pack(params WireField[] f){return RecordingIndex.Join(f);}
    static void Assert(bool ok,string label){if(!ok)throw new Exception("Index test failed: "+label);}
    public static void Run(string baseDir){
        string a="bg_123_20200101_120000",b="bg_123_20200102_120000",c="clip_123_20200101_120000";
        var recordA=RecordingIndex.Message(5,Pack(Text(1,a),Text(99,"unknown recording bytes")));
        var recordB=RecordingIndex.Message(5,Pack(Text(1,b)));
        var timeline=RecordingIndex.Message(1,Pack(Text(1,"timeline_test"),recordA,recordB,Text(98,"phase or future field")));
        var emptyTimeline=RecordingIndex.Message(1,Pack(Text(1,"timeline_no_video"),Text(98,"achievement")));
        var events=Text(2,"untouched postgame bytes");var unknown=Text(100,"future top-level bytes");
        var tag=RecordingIndex.Message(4,Pack(Text(90,"keep tag metadata"),RecordingIndex.Message(3,Pack(Text(1,c),Text(2,"timeline_test"))),RecordingIndex.Message(3,Pack(Text(2,"timeline_test")))));
        byte[] before=Pack(timeline,emptyTimeline,events,Text(3,c),tag,unknown);
        var noop=RecordingIndex.Edit(before,new string[0]);Assert(noop.After.SequenceEqual(before),"no-op exact bytes");
        var plan=RecordingIndex.Edit(before,new[]{a});Assert(plan.RecordingRefs==1 && plan.Timelines==2,"remove selected reference only");
        var output=RecordingIndex.Parse(plan.After);Assert(output.Any(f=>f.Raw.SequenceEqual(emptyTimeline.Raw)) && output.Any(f=>f.Raw.SequenceEqual(events.Raw)) && output.Any(f=>f.Raw.SequenceEqual(unknown.Raw)),"keep empty timelines and unknown root fields");
        var children=RecordingIndex.Parse(output[0].Data);Assert(children.Any(f=>f.Raw.SequenceEqual(recordB.Raw)) && children.Any(f=>f.Number==98),"keep other recordings and unknown child fields");
        var second=RecordingIndex.Edit(plan.After,new[]{a});Assert(!second.Changed,"idempotent");
        var clip=RecordingIndex.Edit(before,new[]{c});Assert(clip.ClipRefs==2 && clip.RecordingRefs==0 && RecordingIndex.Parse(clip.After).First(f=>f.Number==1).Raw.SequenceEqual(timeline.Raw),"clip references removed without touching source timeline");
        var missing=RecordingIndex.Edit(before,new string[0],new HashSet<string>{b},new HashSet<string>{c});Assert(missing.RecordingRefs==1 && missing.ClipRefs==0,"missing references only");
        byte[] unconfirmedClip=Pack(tag);Assert(!RecordingIndex.Edit(unconfirmedClip,new string[0],new HashSet<string>(),new HashSet<string>()).Changed,"unconfirmed remote/shared clip tags retained");
        byte[] remote=Pack(RecordingIndex.Message(1,Pack(Text(1,"timeline_remote"),RecordingIndex.Message(5,Pack(Text(1,a),Text(8,"https://example.invalid/manifest"))))));
        Assert(!RecordingIndex.Edit(remote,new string[0],new HashSet<string>(),new HashSet<string>()).Changed,"remote recording retained");
        bool refused=false;try{RecordingIndex.Edit(remote,new[]{a});}catch(IOException){refused=true;}Assert(refused,"explicit remote edit refused");
        refused=false;try{RecordingIndex.Parse(new byte[]{10,255});}catch(InvalidDataException){refused=true;}Assert(refused,"truncation refused");
        string root=System.IO.Path.Combine(baseDir,"index-write-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(System.IO.Path.Combine(root,"video"));Directory.CreateDirectory(System.IO.Path.Combine(root,"clips"));string path=System.IO.Path.Combine(root,"gamerecording.pb");File.WriteAllBytes(path,before);
        string history=System.IO.Path.Combine(root,"history");RecordingIndex.CommitForTest(root,plan,history,()=>false);Assert(File.ReadAllBytes(path).SequenceEqual(plan.After),"atomic commit");Assert(File.ReadAllBytes(Directory.GetFiles(history,"*.pb")[0]).SequenceEqual(before),"exact backup");
        refused=false;try{RecordingIndex.CommitForTest(root,plan,history,()=>false);}catch(IOException){refused=true;}Assert(refused,"stale snapshot refused");
        File.WriteAllBytes(path,before);refused=false;try{RecordingIndex.CommitForTest(root,plan,history,()=>true);}catch(IOException){refused=true;}Assert(refused && File.ReadAllBytes(path).SequenceEqual(before),"Steam running leaves index unchanged");
        File.WriteAllText(System.IO.Path.Combine(baseDir,"index-test-result.txt"),"PASS: lossless no-op; selected recording only; unknown-field preservation; retain empty/shared timelines and events; clip-tag cleanup; idempotency; missing-file repair; remote reference protection; malformed input rejection; atomic replacement; exact backup; concurrent-change refusal; Steam-running refusal. Only generated fixture data was modified.");
    }
}
}
