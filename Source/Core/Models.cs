namespace RaidCheck.Core;

public sealed record PreviewChoice(string FactionId, string StrategyId, string ArrivalId, int SampleCount);
public sealed record PreviewSnapshot(PreviewChoice Choice, string MapLabel, int GameTicks,
    string FactionLabel, string StrategyLabel, string ArrivalLabel, float BasePoints, int Seed);
public sealed record PawnEntry(string DefName, string Label, float CombatPower);
public sealed record SampleResult(float AdjustedPoints, IReadOnlyList<PawnEntry> Pawns);
public sealed record KindSummary(string DefName, string Label, double MeanCount, int PresentInSamples);
public sealed record PreviewFailure(string Code);
public enum PreviewState { Idle, Running, Completed, Cancelled, Failed }
public sealed class PreviewException(string code) : Exception(code) { public string Code { get; } = code; }
public interface IPreviewSource
{
    PreviewSnapshot Capture(PreviewChoice choice);
    SampleResult Sample(PreviewSnapshot snapshot, int sampleIndex);
}
public sealed record PreviewSummary(int Minimum, double Median, int Maximum, IReadOnlyList<KindSummary> Kinds)
{
    public static PreviewSummary Create(IReadOnlyList<SampleResult> samples)
    {
        if (samples.Count == 0) throw new ArgumentException("No successful samples", nameof(samples));
        var counts = samples.Select(s => s.Pawns.Count).Order().ToArray();
        int middle = counts.Length / 2;
        double median = counts.Length % 2 == 1 ? counts[middle] : (counts[middle-1] + counts[middle]) / 2d;
        var totals = new Dictionary<string, (string Label, int Count, int Presence)>(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pawn in sample.Pawns)
            {
                var old = totals.TryGetValue(pawn.DefName, out var value) ? value : (pawn.Label, 0, 0);
                totals[pawn.DefName] = (old.Item1, old.Item2 + 1, old.Item3 + (seen.Add(pawn.DefName) ? 1 : 0));
            }
        }
        var kinds = totals.Select(p => new KindSummary(p.Key, p.Value.Label, (double)p.Value.Count / samples.Count, p.Value.Presence))
            .OrderByDescending(k => k.MeanCount).ThenBy(k => k.DefName, StringComparer.Ordinal).ToArray();
        return new(counts[0], median, counts[^1], Array.AsReadOnly(kinds));
    }
}
public static class SampleMaterializer
{
    public static SampleResult Read(IEnumerable<PawnEntry> entries, float adjustedPoints, int limit=10000)
    {
        if (!float.IsFinite(adjustedPoints) || adjustedPoints <= 0 || limit < 1)
            throw new PreviewException("RC_Error_Points");
        var pawns = new List<PawnEntry>();
        foreach (var pawn in entries)
        {
            if (pawns.Count >= limit) throw new PreviewException("RC_Error_TooMany");
            if (pawn is null || string.IsNullOrWhiteSpace(pawn.DefName) ||
                !float.IsFinite(pawn.CombatPower) || pawn.CombatPower <= 0)
                throw new PreviewException("RC_Error_PawnKind");
            pawns.Add(pawn);
        }
        if (pawns.Count == 0) throw new PreviewException("RC_Error_Empty");
        return new(adjustedPoints, pawns.AsReadOnly());
    }
}
