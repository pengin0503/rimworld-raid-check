using RaidCheck;
using Verse;
using UnityEngine;
using static RaidCheck.Tests.Program;

namespace RaidCheck.Tests;

static class UiTests
{
    internal static void Run()
    {
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
    }
}
