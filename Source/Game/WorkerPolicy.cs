using RimWorld;

namespace RaidCheck.Game;

// An exact allowlist: a subclass can override kind-only checks with arbitrary side effects.
public static class WorkerPolicy
{
    private static readonly HashSet<string> Strategies = new(StringComparer.Ordinal)
    {
        "RimWorld.RaidStrategyWorker_ImmediateAttack", "RimWorld.RaidStrategyWorker_ImmediateAttackSmart",
        "RimWorld.RaidStrategyWorker_StageThenAttack", "RimWorld.RaidStrategyWorker_Siege",
        "RimWorld.RaidStrategyWorker_ImmediateAttackSappers", "RimWorld.RaidStrategyWorker_ImmediateAttackBreaching",
        "RimWorld.RaidStrategyWorker_ImmediateAttackBreachingSmart"
    };
    private static readonly HashSet<string> Arrivals = new(StringComparer.Ordinal)
    {
        "RimWorld.PawnsArrivalModeWorker_EdgeWalkIn", "RimWorld.PawnsArrivalModeWorker_EdgeWalkInGroups",
        "RimWorld.PawnsArrivalModeWorker_EdgeWalkInDistributed", "RimWorld.PawnsArrivalModeWorker_EdgeWalkInDistributedGroups",
        "RimWorld.PawnsArrivalModeWorker_EdgeDrop", "RimWorld.PawnsArrivalModeWorker_EdgeDropGroups",
        "RimWorld.PawnsArrivalModeWorker_CenterDrop", "RimWorld.PawnsArrivalModeWorker_RandomDrop",
        "RimWorld.PawnsArrivalModeWorker_EmergeFromWater", "RimWorld.PawnsArrivalModeWorker_EdgeWalkInDarkness"
    };
    public static bool SupportsStrategy(Type? type) => type is not null &&
        type.Assembly == typeof(RaidStrategyWorker).Assembly && Strategies.Contains(type.FullName ?? "");
    public static bool SupportsArrival(Type? type) => type is not null &&
        type.Assembly == typeof(PawnsArrivalModeWorker).Assembly && Arrivals.Contains(type.FullName ?? "");
}
