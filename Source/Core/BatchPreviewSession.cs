namespace RaidCheck.Core;
public sealed record BatchItem(PreviewChoice Choice, PreviewSession Session);
public sealed class BatchPreviewSession(Func<IPreviewSource> factory)
{
    public PreviewState State { get; private set; }
    public List<BatchItem> Items { get; } = [];
    private PreviewChoice[] pending = [];
    public IReadOnlyList<PreviewChoice> Choices => pending;
    private int next;
    public bool Check(IReadOnlyList<PreviewChoice> choices)
    {
        if (State == PreviewState.Running || choices.Count == 0 || choices.Any(c => string.IsNullOrEmpty(c.FactionId)) || choices.Select(c => c.FactionId).Distinct(StringComparer.Ordinal).Count() != choices.Count) return false;
        pending = choices.ToArray(); next = 0; Items.Clear(); State = PreviewState.Running;
        StartNext(); return true;
    }
    public void Advance(bool contextValid)
    {
        if (State != PreviewState.Running) return;
        if (!contextValid) { Cancel(); return; }
        if (Items.Count > 0 && Items[^1].Session.State == PreviewState.Running) Items[^1].Session.Advance(true);
        if (Items.Count == 0 || Items[^1].Session.State != PreviewState.Running) StartNext();
    }
    public void Cancel()
    {
        if (State != PreviewState.Running) return;
        if (Items.Count > 0) Items[^1].Session.Cancel();
        State = PreviewState.Cancelled;
    }
    private void StartNext()
    {
        if (next >= pending.Length) { State = Items.Any(i => i.Session.Summary is not null) ? PreviewState.Completed : PreviewState.Failed; return; }
        var choice = pending[next++]; var session = new PreviewSession(factory());
        Items.Add(new(choice, session)); session.Check(choice);
    }
}
