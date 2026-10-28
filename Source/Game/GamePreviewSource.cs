using RaidCheck.Core;
using RimWorld;
using Verse;
namespace RaidCheck.Game;

public sealed class GamePreviewSource : IPreviewSource
{
    private sealed record Context(Verse.Game Game, Map Map, Faction Faction, RaidStrategyDef Strategy, PawnsArrivalModeDef Arrival);
    private Context? context;
    private PreviewSnapshot? capturedSnapshot;

    public PreviewSnapshot Capture(PreviewChoice choice)
    {
        context = null; capturedSnapshot = null;
        int seed = Guid.NewGuid().GetHashCode(); // Does not consume Verse.Rand.
        return RandomIsolation.Run(VerseRandomState.Instance, seed, () =>
        {
            var game = Current.Game;
            var map = Find.CurrentMap;
            if (game is null || map is null) throw new PreviewException("RC_Error_NoMap");
            var faction = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == choice.FactionId);
            if (faction is null || faction.IsPlayer || faction.defeated || faction.deactivated || !faction.HostileTo(Faction.OfPlayer))
                throw new PreviewException("RC_Error_Faction");
            if (faction.def.pawnGroupMakers is null || !faction.def.pawnGroupMakers.Any(g => g.kindDef == PawnGroupKindDefOf.Combat))
                throw new PreviewException("RC_Error_NoCombat");
            if (PawnGroupKindDefOf.Combat.workerClass != typeof(PawnGroupKindWorker_Normal))
                throw new PreviewException("RC_Error_CustomWorker");
            var strategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(choice.StrategyId);
            var arrival = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail(choice.ArrivalId);
            if (strategy is null || arrival is null) throw new PreviewException("RC_Error_Choice");
            if (!WorkerPolicy.SupportsStrategy(strategy.workerClass) || !WorkerPolicy.SupportsArrival(arrival.workerClass))
                throw new PreviewException("RC_Error_CustomWorker");
            if (strategy.arriveModes is null || !strategy.arriveModes.Contains(arrival) || faction.def.techLevel < arrival.minTechLevel)
                throw new PreviewException("RC_Error_Incompatible");

            float points = StorytellerUtility.DefaultThreatPointsNow(map);
            if (!float.IsFinite(points) || points <= 0) throw new PreviewException("RC_Error_Points");
            var newContext = new Context(game, map, faction, strategy, arrival);
            var parms = MakeParms(newContext, points, seed);
            if (!strategy.Worker.CanUseWith(parms, PawnGroupKindDefOf.Combat) ||
                !arrival.Worker.CanUseWith(parms) || !arrival.Worker.CanUseOnMap(map))
                throw new PreviewException("RC_Error_Incompatible");
            var snapshot = new PreviewSnapshot(choice, map.Parent?.LabelCap.ToString() ?? map.GetUniqueLoadID(),
                Find.TickManager.TicksGame, faction.Name, strategy.defName, arrival.defName, points, seed);
            context = newContext; capturedSnapshot = snapshot;
            return snapshot;
        });
    }

    public SampleResult Sample(PreviewSnapshot snapshot, int sampleIndex)
    {
        if (!ReferenceEquals(snapshot, capturedSnapshot) || !ContextMatches())
            throw new PreviewException("RC_Error_Context");
        var c = context!;
        int seed = unchecked(snapshot.Seed + sampleIndex * 1640531527);
        return RandomIsolation.Run(VerseRandomState.Instance, seed, () =>
        {
            // The game applies strategy/arrival curves and minimum group costs in this API.
            var parms = MakeParms(c, snapshot.BasePoints, seed);
            parms.points = IncidentWorker_Raid.AdjustedRaidPoints(snapshot.BasePoints, c.Arrival, c.Strategy,
                c.Faction, PawnGroupKindDefOf.Combat, c.Map, parms.raidAgeRestriction);
            if (!float.IsFinite(parms.points) || parms.points <= 0) throw new PreviewException("RC_Error_Points");
            var group = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, parms, true);
            var cached = new Dictionary<(string Id, string Label, float Power), PawnEntry>();
            var entries = PawnGroupMakerUtility.GeneratePawnKindsExample(group).Select(kind =>
            {
                var key = (kind.defName, kind.label ?? kind.defName, kind.combatPower);
                if (!cached.TryGetValue(key, out var pawn))
                    cached.Add(key, pawn = new PawnEntry(key.Item1, key.Item2, key.Item3));
                return pawn;
            });
            // Full lazy enumeration, including its exceptions, remains inside PushState/finally/PopState.
            return SampleMaterializer.Read(entries, parms.points);
        });
    }

    public bool ContextMatches() => context is not null &&
        ReferenceEquals(context.Game, Current.Game) && ReferenceEquals(context.Map, Find.CurrentMap);

    private static IncidentParms MakeParms(Context c, float points, int seed) => new()
    {
        target = c.Map, faction = c.Faction, points = points, raidStrategy = c.Strategy,
        raidArrivalMode = c.Arrival, pawnGroupMakerSeed = seed, generateFightersOnly = true
    };

    private sealed class VerseRandomState : IRandomState
    {
        internal static readonly VerseRandomState Instance = new();
        public void Push(int seed) => Rand.PushState(seed);
        public void Pop() => Rand.PopState();
    }
}
