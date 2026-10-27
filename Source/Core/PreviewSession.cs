namespace RaidCheck.Core;

public sealed class PreviewSession(IPreviewSource source)
{
    private readonly IPreviewSource source = source;
    private PreviewSummaryAccumulator accumulator = new();
    public PreviewState State { get; private set; }
    public PreviewSnapshot? Snapshot { get; private set; }
    public List<SampleResult> Results { get; } = [];
    public List<PreviewFailure> Failures { get; } = [];
    public PreviewSummary? Summary { get; private set; }
    public Exception? UnexpectedError { get; private set; }
    public int Attempted => Results.Count + Failures.Count;
    public bool Check(PreviewChoice choice)
    {
        if (State == PreviewState.Running) return false;
        Results.Clear(); Failures.Clear(); Snapshot = null; Summary = null; UnexpectedError = null; accumulator = new();
        try
        {
            if (choice.SampleCount is not (10 or 25 or 50)) throw new PreviewException("RC_Error_Samples");
            var captured = source.Capture(choice);
            if (!float.IsFinite(captured.BasePoints) || captured.BasePoints <= 0)
                throw new PreviewException("RC_Error_Points");
            Snapshot = captured;
            State = PreviewState.Running;
            return true;
        }
        catch (Exception ex) { RecordFailure(ex); State = PreviewState.Failed; return false; }
    }
    public void Advance(bool contextValid)
    {
        if (State != PreviewState.Running) return;
        if (!contextValid) { Cancel(); return; }
        try
        {
            var result = source.Sample(Snapshot!, Attempted);
            var validated = SampleMaterializer.Read(result.Pawns, result.AdjustedPoints);
            accumulator.Add(validated);
            Results.Add(validated);
        }
        catch (Exception ex) { RecordFailure(ex); }
        if (Attempted >= Snapshot!.Choice.SampleCount)
        {
            State = Results.Count == 0 ? PreviewState.Failed : PreviewState.Completed;
            Summarize();
        }
    }
    public void Cancel()
    {
        if (State != PreviewState.Running) return;
        State = PreviewState.Cancelled;
        Summarize();
    }
    private void Summarize() => Summary = Results.Count == 0 ? null : accumulator.Build();
    private void RecordFailure(Exception ex)
    {
        if (ex is not PreviewException) UnexpectedError ??= ex;
        Failures.Add(new(ex is PreviewException known ? known.Code : "RC_Error_Unexpected"));
    }
}
