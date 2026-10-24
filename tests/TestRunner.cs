using System;
using System.IO;
namespace SteamRecordManager {
public static class TestRunner {
    [STAThread] public static int Main(string[] args){
        try{
            string root=Path.Combine(Path.GetTempPath(),"srm-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Core.NoLink(root);
            Tests.Run(root);Console.WriteLine("PASS: recording scan, retention, path bounds, change detection, appinfo and language regression suite");
            IndexTests.Run(root);Console.WriteLine("PASS: index preservation, editing, repair, remote-reference checks, atomic commit and backup suite");
            SecurityTests.Run(root,args);Console.WriteLine("PASS: CSV injection defense, export bounds, overwrite refusal, parser limits, junction rejection and release API checks");
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
}
