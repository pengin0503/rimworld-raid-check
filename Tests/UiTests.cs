using RaidCheck;
using Verse;
using UnityEngine;
using static RaidCheck.Tests.Program;

namespace RaidCheck.Tests;

static class UiTests
{
    internal static void Run()
    {
        Test("CSV button exports completed snapshot without another calculation",()=>{
            GameFixture.Reset();string dir=Path.Combine(Path.GetTempPath(),"raid-ui-"+Guid.NewGuid().ToString("N"));GenFilePaths.SaveDataFolderPath=dir;
            try{var w=new MainTabWindow_RaidCheck();w.PostOpen();Widgets.Click="RC_Check";w.DoWindowContents(new Rect(0,0,1000,700));for(int i=0;i<25;i++)w.WindowUpdate();
                Widgets.Click="RC_ExportCsv";w.DoWindowContents(new Rect(0,0,1000,700));var files=Directory.GetFiles(Path.Combine(dir,"RaidCheckExports"),"*.csv");Equal(1,files.Length);True(File.ReadAllText(files[0]).Contains("Pirates"));Equal(1,GameFixture.PointReads);Equal(25,GameFixture.Previews);
            }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);GenFilePaths.SaveDataFolderPath=Path.GetTempPath();}
        });
        Test("single selected faction label matches actual selection",()=>{
            GameFixture.Reset();Find.FactionManager.AllFactionsListForReading.Add(new RimWorld.Faction{Id="tribal",Name="Tribals",def=new(){pawnGroupMakers=[new()]}});
            var w=new MainTabWindow_RaidCheck();w.PostOpen();Widgets.Click="RC_SelectMultiple";w.DoWindowContents(new Rect(0,0,1000,700));Find.WindowStack.LastMenu!.Options.Single(o=>o.Label.Contains("Tribals")).Action!();
            Widgets.Click="RC_SelectMultiple";w.DoWindowContents(new Rect(0,0,1000,700));Find.WindowStack.LastMenu!.Options.Single(o=>o.Label.Contains("Pirates")).Action!();
            Widgets.ButtonLabels.Clear();w.DoWindowContents(new Rect(0,0,1000,700));True(Widgets.ButtonLabels.Contains("RC_Faction: Tribals"));
        });
        Test("cancelled and unstarted factions are not reported as generation failures",()=>{
            GameFixture.Reset();foreach(var name in new[]{"Tribal","Zulu"})Find.FactionManager.AllFactionsListForReading.Add(new RimWorld.Faction{Id=name,Name=name,def=new(){pawnGroupMakers=[new()]}});
            TestTranslations.Values["RC_ComparisonFailed"]="{0}: {1}";
            try {var w=new MainTabWindow_RaidCheck();w.PostOpen();foreach(var name in new[]{"Tribal","Zulu"}){Widgets.Click="RC_SelectMultiple";w.DoWindowContents(new Rect(0,0,1000,700));Find.WindowStack.LastMenu!.Options.Single(o=>o.Label.Contains(name)).Action!();}
                Widgets.Click="RC_Check";w.DoWindowContents(new Rect(0,0,1000,700));for(int i=0;i<25;i++)w.WindowUpdate();Widgets.Click="RC_Cancel";w.DoWindowContents(new Rect(0,0,1000,700));Widgets.Labels.Clear();w.DoWindowContents(new Rect(0,0,1000,700));var text=string.Join("\n",Widgets.Labels);
                True(text.Contains("Tribal: RC_State_Cancelled"));True(text.Contains("Zulu: RC_NotStarted"));True(!text.Contains("Tribal: RC_Error_Empty"));
            } finally {TestTranslations.Values.Clear();}
        });
        Test("opening and idle rendering never read points or sample",()=>{
            GameFixture.Reset();var window=new MainTabWindow_RaidCheck();window.PostOpen();
            for(int i=0;i<10;i++){window.WindowUpdate();window.DoWindowContents(new Rect(0,0,1000,700));}
            Equal(0,GameFixture.PointReads);Equal(0,GameFixture.Previews);True(window.forcePause);
        });
        Test("Check click starts once and finished rendering never recomputes",()=>{
            GameFixture.Reset();var window=new MainTabWindow_RaidCheck();window.PostOpen();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));Equal(1,GameFixture.PointReads);Equal(0,GameFixture.Previews);
            for(int i=0;i<60;i++){window.WindowUpdate();window.DoWindowContents(new Rect(0,0,1000,700));}
            Equal(25,GameFixture.Previews);Equal(1,GameFixture.PointReads);
        });
        Test("closing an active window stops later samples",()=>{
            GameFixture.Reset();var window=new MainTabWindow_RaidCheck();window.PostOpen();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));window.WindowUpdate();window.PreClose();
            for(int i=0;i<30;i++)window.WindowUpdate();Equal(1,GameFixture.Previews);
        });
        Test("switching map cancels an active UI session",()=>{
            GameFixture.Reset();var window=new MainTabWindow_RaidCheck();window.PostOpen();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));window.WindowUpdate();Find.CurrentMap=new();
            for(int i=0;i<30;i++)window.WindowUpdate();Equal(1,GameFixture.Previews);
        });
        Test("control changes do not silently relabel captured results or recalculate",()=>{
            GameFixture.Reset();var window=new MainTabWindow_RaidCheck();window.PostOpen();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));for(int i=0;i<25;i++)window.WindowUpdate();
            Widgets.Click="RC_Samples: 25";window.DoWindowContents(new Rect(0,0,1000,700));
            True(Find.WindowStack.LastMenu is not null);Find.WindowStack.LastMenu!.Options.First().Action!();
            Widgets.Labels.Clear();window.DoWindowContents(new Rect(0,0,1000,700));
            True(Widgets.Labels.Any(x=>x.Contains("RC_ConditionsChanged")));Equal(1,GameFixture.PointReads);Equal(25,GameFixture.Previews);
        });
        Test("unchanged results do not repeatedly measure a long report",()=>{
            GameFixture.Reset();var window=new MainTabWindow_RaidCheck();window.PostOpen();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));for(int i=0;i<25;i++)window.WindowUpdate();
            window.DoWindowContents(new Rect(0,0,1000,700));int measured=Text.Measurements;
            for(int i=0;i<20;i++)window.DoWindowContents(new Rect(0,0,1000,700));Equal(measured,Text.Measurements);
            window.DoWindowContents(new Rect(0,0,900,700));Equal(measured+1,Text.Measurements);
        });
        Test("actual translated report retains captured faction, modes, counts and point budget",()=>{
            GameFixture.Reset();
            var root=new DirectoryInfo(Directory.GetCurrentDirectory());
            while(root is not null && !File.Exists(Path.Combine(root.FullName,"Mod/About/About.xml")))root=root.Parent;
            True(root is not null);
            try{
                foreach(string language in new[]{"English","Japanese"}){
                    TestTranslations.Values=System.Xml.Linq.XDocument.Load(Path.Combine(root!.FullName,$"Mod/Languages/{language}/Keyed/RaidCheck.xml")).Root!.Elements().ToDictionary(e=>e.Name.LocalName,e=>e.Value);
                    var window=new MainTabWindow_RaidCheck();window.PostOpen();Widgets.Click=TestTranslations.Values["RC_Check"];
                    window.DoWindowContents(new Rect(0,0,1000,700));for(int i=0;i<25;i++)window.WindowUpdate();
                    Widgets.Labels.Clear();window.DoWindowContents(new Rect(0,0,1000,700));
                    var rendered=string.Join("\n",Widgets.Labels);
                    foreach(string value in new[]{"Home","Pirates","25/25","640","Lancer"})True(rendered.Contains(value));
                    True(rendered.Contains(TestTranslations.Values["RC_Strategy_Siege"]));
                    True(rendered.Contains(TestTranslations.Values["RC_Arrival_EdgeDrop"]));
                }
            }finally{TestTranslations.Values.Clear();}
        });
        Test("an unexpected sample failure is logged once when closing before completion",()=>{
            GameFixture.Reset();Log.Errors=0;GameFixture.Kinds=()=>throw new InvalidOperationException("native error");
            var window=new MainTabWindow_RaidCheck();window.PostOpen();Widgets.Click="RC_Check";
            window.DoWindowContents(new Rect(0,0,1000,700));window.WindowUpdate();Equal(0,Log.Errors);
            window.PreClose();Equal(1,Log.Errors);window.PreClose();Equal(1,Log.Errors);
        });
        Test("idle game replacement refreshes faction choices",()=>{
            GameFixture.Reset();var window=new MainTabWindow_RaidCheck();window.PostOpen();
            Current.Game=new();Find.CurrentMap=new();Find.FactionManager=new();
            Find.FactionManager.AllFactionsListForReading.Add(new RimWorld.Faction{Id="new",Name="New faction",def=new(){pawnGroupMakers=[new()]}});
            window.WindowUpdate();Widgets.Click="RC_Faction: New faction";window.DoWindowContents(new Rect(0,0,1000,700));
            True(Find.WindowStack.LastMenu is not null);Equal("New faction",Find.WindowStack.LastMenu!.Options[0].Label);Equal(0,GameFixture.PointReads);
        });
        Test("multiple factions can be checked sequentially and compared",()=>{
            GameFixture.Reset();Find.FactionManager.AllFactionsListForReading.Add(new RimWorld.Faction{Id="tribal",Name="Tribals",def=new(){pawnGroupMakers=[new()]}});
            var window=new MainTabWindow_RaidCheck();window.PostOpen();Widgets.Click="RC_SelectMultiple";
            window.DoWindowContents(new Rect(0,0,1000,700));
            var option=Find.WindowStack.LastMenu!.Options.Single(o=>o.Label.Contains("Tribals"));option.Action!();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));
            for(int i=0;i<60;i++)window.WindowUpdate();
            Equal(50,GameFixture.Previews);Equal(2,GameFixture.PointReads);
            Widgets.Click="RC_ViewFaction";window.DoWindowContents(new Rect(0,0,1000,700));
            True(Find.WindowStack.LastMenu!.Options.Any(o=>o.Label=="Pirates"));
            True(Find.WindowStack.LastMenu.Options.Any(o=>o.Label=="Tribals"));
        });
        Test("old faction menu cannot change selection after game replacement",()=>{
            GameFixture.Reset();Find.FactionManager.AllFactionsListForReading.Add(new RimWorld.Faction{Id="old2",Name="Old2",def=new(){pawnGroupMakers=[new()]}});var window=new MainTabWindow_RaidCheck();window.PostOpen();Widgets.Click="RC_SelectMultiple";window.DoWindowContents(new Rect(0,0,1000,700));
            var old=Find.WindowStack.LastMenu!.Options.Select(o=>o.Action!).ToArray();Current.Game=new();Find.CurrentMap=new();Find.FactionManager=new();window.WindowUpdate();foreach(var action in old)action();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));Equal(0,GameFixture.PointReads);
        });
        Test("failed second faction logs error without claiming map changed",()=>{
            GameFixture.Reset();Log.Errors=0;Find.FactionManager.AllFactionsListForReading.Add(new RimWorld.Faction{Id="tribal",Name="Tribals",def=new(){pawnGroupMakers=[new()]}});
            var window=new MainTabWindow_RaidCheck();window.PostOpen();Widgets.Click="RC_SelectMultiple";window.DoWindowContents(new Rect(0,0,1000,700));Find.WindowStack.LastMenu!.Options.Single(o=>o.Label.Contains("Tribals")).Action!();
            GameFixture.ThrowForFaction="tribal";Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));for(int i=0;i<30;i++)window.WindowUpdate();
            Widgets.Labels.Clear();window.DoWindowContents(new Rect(0,0,1000,700));True(!Widgets.Labels.Any(s=>s.Contains("RC_ContextChanged")));Equal(1,Log.Errors);
        });
        Test("closing a batch prevents queued faction from starting",()=>{
            GameFixture.Reset();Find.FactionManager.AllFactionsListForReading.Add(new RimWorld.Faction{Id="tribal",Name="Tribals",def=new(){pawnGroupMakers=[new()]}});
            var window=new MainTabWindow_RaidCheck();window.PostOpen();Widgets.Click="RC_SelectMultiple";window.DoWindowContents(new Rect(0,0,1000,700));Find.WindowStack.LastMenu!.Options.Single(o=>o.Label.Contains("Tribals")).Action!();
            Widgets.Click="RC_Check";window.DoWindowContents(new Rect(0,0,1000,700));window.WindowUpdate();window.PreClose();for(int i=0;i<60;i++)window.WindowUpdate();
            Equal(1,GameFixture.PointReads);Equal(1,GameFixture.Previews);
        });
    }
}
