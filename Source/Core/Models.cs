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
        var accumulator = new PreviewSummaryAccumulator();
        foreach (var sample in samples) accumulator.Add(sample);
        return accumulator.Build();
    }
}
public static class SampleMaterializer
{
    public static SampleResult Read(IEnumerable<PawnEntry> entries, float adjustedPoints, int limit=10000)
    {
        if (!float.IsFinite(adjustedPoints) || adjustedPoints <= 0 || limit < 1)
            throw new PreviewException("RC_Error_Points");
        if (entries is CompressedPawnList compressed)
        {
            if (compressed.Count > limit) throw new PreviewException("RC_Error_TooMany");
            return new(adjustedPoints, compressed);
        }
        var kinds = new Dictionary<PawnEntry, int>();
        int count = 0;
        foreach (var pawn in entries)
        {
            if (count >= limit) throw new PreviewException("RC_Error_TooMany");
            if (pawn is null || string.IsNullOrWhiteSpace(pawn.DefName) ||
                !float.IsFinite(pawn.CombatPower) || pawn.CombatPower <= 0)
                throw new PreviewException("RC_Error_PawnKind");
            kinds.TryGetValue(pawn, out var existing);
            kinds[pawn] = existing + 1;
            count++;
        }
        if (count == 0) throw new PreviewException("RC_Error_Empty");
        return new(adjustedPoints, new CompressedPawnList(kinds, count));
    }
}

internal sealed class CompressedPawnList(Dictionary<PawnEntry, int> kinds, int count) : IReadOnlyList<PawnEntry>
{
    private readonly KeyValuePair<PawnEntry, int>[] groups = kinds.ToArray();
    public int Count { get; } = count;
    public PawnEntry this[int index]
    {
        get
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            foreach (var group in groups) { if (index < group.Value) return group.Key; index -= group.Value; }
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
    public IEnumerator<PawnEntry> GetEnumerator()
    {
        foreach (var group in groups) for (int i = 0; i < group.Value; i++) yield return group.Key;
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
