// Narrow contract doubles, NOT RimWorld implementations or real-game validation.
using RaidCheck.Tests;
namespace Verse
{
    public class Def { public string defName=""; public string? label; public string LabelCap=>label??defName; }
    public class Map { public ParentInfo Parent=new(); public int Tile=3; public string GetUniqueLoadID()=>"map1"; }
    public class ParentInfo { public string LabelCap=>"Home"; }
    public class Game { }
    public static class GenFilePaths { public static string SaveDataFolderPath=Path.GetTempPath(); }
    public static class Current { public static Game? Game=new(); }
    public class TickManager { public int TicksGame=12500; }
    public static class Find
    {
        public static Map? CurrentMap=new();
        public static RimWorld.FactionManager FactionManager=new();
        public static TickManager TickManager=new();
        public static WindowStack WindowStack=new();
    }
    public class PawnKindDef:Def { public float combatPower=50; }
    public static class DefDatabase<T> where T:Def
    {
        public static List<T> AllDefsListForReading=[];
        public static T? GetNamedSilentFail(string name)=>AllDefsListForReading.FirstOrDefault(d=>d.defName==name);
    }
    public static class Rand
    {
        public static int Depth, Seed, Pops;
        public static void PushState(int seed){Depth++;Seed=seed;}
        public static void PopState(){Depth--;Pops++;}
    }
}
namespace RimWorld
{
    public enum TechLevel { Neolithic=2,Industrial=4,Spacer=5 }
    public class FactionDef:Verse.Def { public List<PawnGroupMaker> pawnGroupMakers=[];public TechLevel techLevel=TechLevel.Industrial; }
    public class Faction
    {
        public string Id="pirates",Name="Pirates";
        public FactionDef def=new(); public bool defeated,deactivated,IsPlayer;
        public bool IsHostile=true;
        public bool HostileTo(Faction? other)=>IsHostile;
        public string GetUniqueLoadID()=>Id;
        public static Faction OfPlayer=new(){IsPlayer=true};
    }
    public class FactionManager { public List<Faction> AllFactionsListForReading=[]; }
    public class PawnGroupMaker { public PawnGroupKindDef kindDef=PawnGroupKindDefOf.Combat; }
    public class PawnGroupKindDef:Verse.Def { public Type workerClass=typeof(PawnGroupKindWorker_Normal); }
    public class PawnGroupKindWorker_Normal { }
    public static class PawnGroupKindDefOf { public static PawnGroupKindDef Combat=new(){defName="Combat"}; }
    public class RaidStrategyDef:Verse.Def
    {
        public Type workerClass=typeof(RaidStrategyWorker_ImmediateAttack);
        public List<PawnsArrivalModeDef> arriveModes=[];
        public RaidStrategyWorker Worker=>new();
    }
    public class RaidStrategyWorker
    {
        public static bool Usable=true;
        public virtual bool CanUseWith(IncidentParms p,PawnGroupKindDef g){GameFixture.StrategyValidation=p;if(p.faction?.Id==GameFixture.ThrowForFaction)throw new InvalidOperationException("faction failure");return Usable;}
    }
    public class RaidStrategyWorker_ImmediateAttack:RaidStrategyWorker { }
    public class RaidStrategyWorker_ImmediateAttackSmart:RaidStrategyWorker { }
    public class RaidStrategyWorker_StageThenAttack:RaidStrategyWorker { }
    public class RaidStrategyWorker_Siege:RaidStrategyWorker { }
    public class RaidStrategyWorker_ImmediateAttackSappers:RaidStrategyWorker { }
    public class RaidStrategyWorker_ImmediateAttackBreaching:RaidStrategyWorker { }
    public class RaidStrategyWorker_ImmediateAttackBreachingSmart:RaidStrategyWorker { }
    public class RaidStrategyWorker_SiegeMechanoid:RaidStrategyWorker { }
    public class PawnsArrivalModeDef:Verse.Def
    {
        public Type workerClass=typeof(PawnsArrivalModeWorker_EdgeWalkIn);
        public TechLevel minTechLevel;
        public PawnsArrivalModeWorker Worker=>new();
    }
    public class PawnsArrivalModeWorker
    {
        public static bool Usable=true;
        public virtual bool CanUseWith(IncidentParms p){GameFixture.ArrivalValidation=p;return Usable;}
        public virtual bool CanUseOnMap(Verse.Map m)=>Usable;
    }
    public class PawnsArrivalModeWorker_EdgeWalkIn:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_EdgeWalkInGroups:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_EdgeWalkInDistributed:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_EdgeWalkInDistributedGroups:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_EdgeDrop:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_EdgeDropGroups:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_CenterDrop:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_RandomDrop:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_EmergeFromWater:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_EdgeWalkInDarkness:PawnsArrivalModeWorker { }
    public class PawnsArrivalModeWorker_ClusterDrop:PawnsArrivalModeWorker { }
    public class RaidAgeRestrictionDef { }
    public class IncidentParms
    {
        public Verse.Map? target; public float points; public Faction? faction;
        public RaidStrategyDef? raidStrategy;public PawnsArrivalModeDef? raidArrivalMode;
        public int? pawnGroupMakerSeed;public RaidAgeRestrictionDef? raidAgeRestriction;
        public bool generateFightersOnly;
    }
    public class PawnGroupMakerParms
    {
        public PawnGroupKindDef? groupKind; public Faction? faction; public float points;
        public RaidStrategyDef? raidStrategy;public int? seed;
    }
    public static class StorytellerUtility
    {
        public static float DefaultThreatPointsNow(Verse.Map map){GameFixture.PointReads++;GameFixture.Guarded();return 1000;}
    }
    public class IncidentWorker_Raid
    {
        public static float AdjustedRaidPoints(float points,PawnsArrivalModeDef arrival,RaidStrategyDef strategy,Faction faction,
            PawnGroupKindDef kind,Verse.Map target,RaidAgeRestrictionDef? age)
        {
            GameFixture.Guarded();GameFixture.Adjustments++;GameFixture.AdjustedChoice=(strategy.defName,arrival.defName);
            return 640; // Sentinel: catches accidental omission/double-application, not a game formula.
        }
    }
    public static class IncidentParmsUtility
    {
        public static PawnGroupMakerParms GetDefaultPawnGroupMakerParms(PawnGroupKindDef kind,IncidentParms p,bool ensure)
        {
            GameFixture.Guarded();GameFixture.GroupParms=p;GameFixture.Ensure=ensure;
            return new(){groupKind=kind,faction=p.faction,points=p.points,raidStrategy=p.raidStrategy,seed=p.pawnGroupMakerSeed};
        }
    }
    public static class PawnGroupMakerUtility
    {
        public static IEnumerable<Verse.PawnKindDef> GeneratePawnKindsExample(PawnGroupMakerParms p)
        {
            GameFixture.Guarded();GameFixture.Previews++;GameFixture.PreviewParms=p;
            foreach(var k in GameFixture.Kinds()){GameFixture.Guarded();yield return k;}
        }
    }
}
