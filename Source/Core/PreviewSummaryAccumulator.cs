namespace RaidCheck.Core;

public sealed class PreviewSummaryAccumulator
{
    private readonly List<int> counts = [];
    private readonly Dictionary<string, (string Label, int Count, int Presence)> totals = new(StringComparer.Ordinal);
    public void Add(SampleResult sample)
    {
        counts.Add(sample.Pawns.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pawn in sample.Pawns)
        {
            var old = totals.TryGetValue(pawn.DefName, out var value) ? value : (pawn.Label, 0, 0);
            totals[pawn.DefName] = (old.Item1, old.Item2 + 1, old.Item3 + (seen.Add(pawn.DefName) ? 1 : 0));
        }
    }
    public PreviewSummary Build()
    {
        if (counts.Count == 0) throw new ArgumentException("No successful samples");
        var ordered = counts.Order().ToArray(); int middle = ordered.Length / 2;
        double median = ordered.Length % 2 == 1 ? ordered[middle] : (ordered[middle-1] + ordered[middle]) / 2d;
        var kinds = totals.Select(p => new KindSummary(p.Key, p.Value.Label, (double)p.Value.Count / counts.Count, p.Value.Presence))
            .OrderByDescending(k => k.MeanCount).ThenBy(k => k.DefName, StringComparer.Ordinal).ToArray();
        return new(ordered[0], median, ordered[^1], Array.AsReadOnly(kinds));
    }
}
