using RaidCheck.Core;
using RaidCheck.Game;
using RimWorld;
using Verse;
using static RaidCheck.Tests.Program;

namespace RaidCheck.Tests;

static class GameFixture
{
    internal static int PointReads,Adjustments,Previews;
    internal static (string Strategy,string Arrival) AdjustedChoice;
    internal static IncidentParms? GroupParms,StrategyValidation,ArrivalValidation;
    internal static PawnGroupMakerParms? PreviewParms;
    internal static bool Ensure;
    internal static string? ThrowForFaction;
    internal static Func<IEnumerable<PawnKindDef>> Kinds=()=>new[]{new PawnKindDef{defName="Lancer",label="lancer",combatPower=150}};
    internal static void Guarded(){if(Rand.Depth==0)throw new Exception("game call outside random isolation");}
    internal static void Reset()
    {
        PointReads=Adjustments=Previews=0;GroupParms=StrategyValidation=ArrivalValidation=null;PreviewParms=null;ThrowForFaction=null;
        Current.Game=new();Find.CurrentMap=new();Find.FactionManager=new();Rand.Depth=Rand.Pops=0;
        RaidStrategyWorker.Usable=PawnsArrivalModeWorker.Usable=true;
        PawnGroupKindDefOf.Combat.workerClass=typeof(PawnGroupKindWorker_Normal);
        var f=new Faction();f.def.pawnGroupMakers.Add(new());Find.FactionManager.AllFactionsListForReading.Add(f);
        var a=new PawnsArrivalModeDef {defName="EdgeDrop",workerClass=typeof(PawnsArrivalModeWorker_EdgeDrop)};
        var s=new RaidStrategyDef {defName="Siege",workerClass=typeof(RaidStrategyWorker_Siege),arriveModes=[a]};
        DefDatabase<PawnsArrivalModeDef>.AllDefsListForReading=[a];DefDatabase<RaidStrategyDef>.AllDefsListForReading=[s];
        Kinds=()=>new[]{new PawnKindDef{defName="Lancer",label="lancer",combatPower=150}};
    }
}
static class AdapterTests
{
    internal static void Run()
    {
        Test("game source is inert until Check",()=>{
            GameFixture.Reset();var source=new GamePreviewSource();var session=new PreviewSession(source);
            for(int i=0;i<10;i++)session.Advance(true);Equal(0,GameFixture.PointReads);Equal(0,GameFixture.Previews);
            True(session.Check(CoreTests.Choice));Equal(1,GameFixture.PointReads);Equal(0,GameFixture.Previews);Equal(0,Rand.Depth);
        });
        Test("all standard strategies reach adjustment and preview without fallback",()=>{
            foreach(var type in new[]{typeof(RaidStrategyWorker_ImmediateAttack),typeof(RaidStrategyWorker_ImmediateAttackSmart),typeof(RaidStrategyWorker_StageThenAttack),typeof(RaidStrategyWorker_Siege),typeof(RaidStrategyWorker_ImmediateAttackSappers),typeof(RaidStrategyWorker_ImmediateAttackBreaching),typeof(RaidStrategyWorker_ImmediateAttackBreachingSmart)}){
                GameFixture.Reset();var strategy=DefDatabase<RaidStrategyDef>.AllDefsListForReading[0];strategy.workerClass=type;strategy.defName=type.Name;
                var src=new GamePreviewSource();var snap=src.Capture(CoreTests.Choice with {StrategyId=strategy.defName});var result=src.Sample(snap,0);
                Equal((strategy.defName,"EdgeDrop"),GameFixture.AdjustedChoice);Equal(1,GameFixture.Adjustments);
                Equal(640f,GameFixture.GroupParms!.points);Equal(640f,GameFixture.PreviewParms!.points);True(GameFixture.Ensure);
                Equal(strategy,GameFixture.PreviewParms.raidStrategy);Equal("Lancer",result.Pawns[0].DefName);Equal(150f,result.Pawns[0].CombatPower);Equal(0,Rand.Depth);
            }
        });
        Test("standard arrivals including drops are passed unchanged",()=>{
            foreach(var type in new[]{typeof(PawnsArrivalModeWorker_EdgeWalkIn),typeof(PawnsArrivalModeWorker_EdgeWalkInGroups),typeof(PawnsArrivalModeWorker_EdgeDrop),typeof(PawnsArrivalModeWorker_EdgeDropGroups),typeof(PawnsArrivalModeWorker_CenterDrop),typeof(PawnsArrivalModeWorker_RandomDrop),typeof(PawnsArrivalModeWorker_EmergeFromWater)}){
                GameFixture.Reset();var arrival=DefDatabase<PawnsArrivalModeDef>.AllDefsListForReading[0];arrival.workerClass=type;arrival.defName=type.Name;
                var src=new GamePreviewSource();var snap=src.Capture(CoreTests.Choice with {ArrivalId=arrival.defName});src.Sample(snap,0);
                Equal(("Siege",arrival.defName),GameFixture.AdjustedChoice);Equal(arrival,GameFixture.ArrivalValidation!.raidArrivalMode);
            }
        });
        Test("incompatible, missing combat, low-tech and custom workers are explicit errors",()=>{
            foreach(var setup in new Action[]{
                ()=>RaidStrategyWorker.Usable=false,
                ()=>PawnsArrivalModeWorker.Usable=false,
                ()=>DefDatabase<RaidStrategyDef>.AllDefsListForReading[0].arriveModes.Clear(),
                ()=>Find.FactionManager.AllFactionsListForReading[0].def.pawnGroupMakers.Clear(),
                ()=>DefDatabase<PawnsArrivalModeDef>.AllDefsListForReading[0].minTechLevel=TechLevel.Spacer,
                ()=>DefDatabase<RaidStrategyDef>.AllDefsListForReading[0].workerClass=typeof(CustomStrategy),
                ()=>DefDatabase<PawnsArrivalModeDef>.AllDefsListForReading[0].workerClass=typeof(PawnsArrivalModeWorker_ClusterDrop),
                ()=>PawnGroupKindDefOf.Combat.workerClass=typeof(object),
                ()=>Find.FactionManager.AllFactionsListForReading[0].IsHostile=false
            }){GameFixture.Reset();setup();Throws<PreviewException>(()=>new GamePreviewSource().Capture(CoreTests.Choice));Equal(0,GameFixture.Previews);Equal(0,Rand.Depth);}
        });
        Test("adapter finishes lazy enumeration inside the seed scope on errors",()=>{
            GameFixture.Reset();IEnumerable<PawnKindDef> Bad(){yield return new(){defName="A"};throw new InvalidOperationException("lazy native");}
            GameFixture.Kinds=Bad;var src=new GamePreviewSource();var snap=src.Capture(CoreTests.Choice);
            Throws<InvalidOperationException>(()=>src.Sample(snap,0));Equal(0,Rand.Depth);Equal(2,Rand.Pops);
        });
        Test("game or map replacement invalidates a captured run",()=>{
            foreach(bool map in new[]{true,false}){GameFixture.Reset();var src=new GamePreviewSource();var snap=src.Capture(CoreTests.Choice);True(src.ContextMatches());if(map)Find.CurrentMap=new();else Current.Game=new();True(!src.ContextMatches());Throws<PreviewException>(()=>src.Sample(snap,0));Equal(0,GameFixture.Previews);}
        });
        Test("each sample has a distinct reproducible seed and snapshot identity",()=>{
            GameFixture.Reset();var src=new GamePreviewSource();var snap=src.Capture(CoreTests.Choice);src.Sample(snap,0);int seed0=Rand.Seed;src.Sample(snap,1);True(seed0!=Rand.Seed);src.Sample(snap,0);Equal(seed0,Rand.Seed);
        });
    }
    private sealed class CustomStrategy:RaidStrategyWorker { }
}
