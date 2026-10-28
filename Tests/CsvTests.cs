using RaidCheck.Core;
using static RaidCheck.Tests.Program;
namespace RaidCheck.Tests;
static class CsvTests
{
    internal static void Run()
    {
        Test("CSV includes captured conditions, kind counts and sample status",()=>{
            var session=new PreviewSession(new CoreTests.Source());session.Check(CoreTests.Choice);for(int i=0;i<10;i++)session.Advance(true);
            using var w=new StringWriter();RaidCsv.Write(w,new[]{new CsvFaction(CoreTests.Choice,"Pirates",session)});var text=w.ToString();
            True(text.Contains("faction_id"));True(text.Contains("successful_sample_index"));True(text.Contains("Pirates"));True(text.Contains("Siege"));True(text.Contains("Completed"));True(text.Contains("kind"));True(text.Contains("sample_kind"));
        });
        Test("CSV escapes quotes, commas, newlines and formula-like labels",()=>{
            var session=new PreviewSession(new CoreTests.Source());session.Check(CoreTests.Choice);session.Advance(true);session.Cancel();
            using var w=new StringWriter();RaidCsv.Write(w,new[]{new CsvFaction(CoreTests.Choice,"=SUM(1,2)\n\"x\"",session)});var text=w.ToString();
            True(text.Contains("\"'=SUM(1,2)\n\"\"x\"\"\""));True(text.Contains("Cancelled"));
        });
        Test("CSV reports unstarted and failed factions without fabricated counts",()=>{
            var c=CoreTests.Choice;var s=new PreviewSession(new CoreTests.Source{CaptureError=new PreviewException("no-map")});s.Check(c);
            using var w=new StringWriter();RaidCsv.Write(w,new[]{new CsvFaction(c,"Failed",s),new CsvFaction(c with {FactionId="next"},"Next",null)});
            var text=w.ToString();True(text.Contains("Failed"));True(text.Contains("NotStarted"));True(text.Contains("no-map"));
        });
        Test("CSV export avoids per-pawn temporary allocations",()=>{
            var sample=SampleMaterializer.Read(Enumerable.Repeat(CoreTests.Pawn(),10000),100);
            var session=new PreviewSession(new CoreTests.Source{Next=_=>sample});session.Check(CoreTests.Choice);
            for(int i=0;i<10;i++)session.Advance(true);
            void Export(){using var w=new StringWriter();RaidCsv.Write(w,new[]{new CsvFaction(CoreTests.Choice,"Pirates",session)});}
            Export();
            long before=GC.GetAllocatedBytesForCurrentThread();Export();
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            True(bytes < 300000);
        });
        Test("CSV file is UTF-8 BOM and uses unique names",()=>{
            string dir=Path.Combine(Path.GetTempPath(),"raid-csv-"+Guid.NewGuid().ToString("N"));
            try{var c=CoreTests.Choice;var records=new[]{new CsvFaction(c,"日本語",null)};string a=RaidCsv.WriteFile(dir,records);string b=RaidCsv.WriteFile(dir,records);True(a!=b);var bytes=File.ReadAllBytes(a);True(bytes.Take(3).SequenceEqual(new byte[]{0xef,0xbb,0xbf}));True(System.Text.Encoding.UTF8.GetString(bytes).Contains("日本語"));}
            finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
        });
    }
}
