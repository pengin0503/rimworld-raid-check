using RimWorld;
using UnityEngine;
using Verse;
using RaidCheck.Core;
using RaidCheck.Game;
using System.Globalization;
using System.Text;

namespace RaidCheck;

public sealed class MainTabWindow_RaidCheck : MainTabWindow
{
    private GamePreviewSource source = new();
    private PreviewSession session;
    private List<Faction> factions = [];
    private PreviewChoice choice = new("", "ImmediateAttack", "EdgeWalkIn", 25);
    private Vector2 scroll;
    private string report = "";
    private string summaryText = "";
    private string[] examples = [];
    private int page;
    private bool loggedError;
    private float measuredWidth = -1, measuredHeight = -1;

    public MainTabWindow_RaidCheck()
    {
        session = new(source);
        forcePause = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 RequestedTabSize => new(Math.Min(1100, UI.screenWidth - 40), Math.Min(780, UI.screenHeight - 100));

    public override void PostOpen()
    {
        base.PostOpen();
        source = new(); session = new(source);
        report = summaryText = ""; examples = []; page = 0; scroll = new(); loggedError = false; measuredHeight = -1;
        factions = Current.Game is null || Find.CurrentMap is null ? [] : Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.defeated && !f.deactivated && f.HostileTo(Faction.OfPlayer) &&
                f.def.pawnGroupMakers?.Any(g => g.kindDef == PawnGroupKindDefOf.Combat) == true)
            .OrderBy(f => f.Name, StringComparer.CurrentCulture).ToList();
        choice = new(factions.FirstOrDefault()?.GetUniqueLoadID() ?? "", "ImmediateAttack", "EdgeWalkIn", 25);
        if (DefDatabase<RaidStrategyDef>.GetNamedSilentFail(choice.StrategyId) is null)
            choice = choice with { StrategyId = DefDatabase<RaidStrategyDef>.AllDefsListForReading.FirstOrDefault(s => WorkerPolicy.SupportsStrategy(s.workerClass))?.defName ?? "" };
        NormalizeArrival();
    }

    public override void PreClose()
    {
        session.Cancel();
        LogUnexpectedOnce();
        base.PreClose();
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();
        if (session.State != PreviewState.Running) return;
        session.Advance(source.ContextMatches()); // At most one sample, only after explicit Check.
        if (session.State != PreviewState.Running) CacheReport();
    }

    public override void DoWindowContents(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new(0, 0, rect.width, 32), T("RC_Title"));
        Text.Font = GameFont.Small;
        Widgets.Label(new(0, 36, rect.width, 42), T("RC_ManualOnly"));
        float half = (rect.width - 12) / 2;
        bool wasEnabled = GUI.enabled;
        try
        {
            GUI.enabled = wasEnabled && session.State != PreviewState.Running;
            if (Widgets.ButtonText(new(0, 84, half, 32), T("RC_Faction") + ": " + (factions.FirstOrDefault(f => f.GetUniqueLoadID() == choice.FactionId)?.Name ?? T("RC_None")))) OpenFactions();
            if (Widgets.ButtonText(new(half + 12, 84, half, 32), T("RC_Strategy") + ": " + StrategyName(choice.StrategyId))) OpenStrategies();
            if (Widgets.ButtonText(new(0, 124, half, 32), T("RC_Arrival") + ": " + ArrivalName(choice.ArrivalId))) OpenArrivals();
            if (Widgets.ButtonText(new(half + 12, 124, half, 32), T("RC_Samples") + ": " + choice.SampleCount))
                Find.WindowStack.Add(new FloatMenu(new[] {10,25,50}.Select(n => new FloatMenuOption(n.ToString(), () => choice = choice with { SampleCount = n })).ToList()));
            GUI.enabled = wasEnabled && session.State != PreviewState.Running && choice.FactionId.Length != 0 && Find.CurrentMap is not null;
            if (Widgets.ButtonText(new(0, 168, 150, 32), T("RC_Check")))
            {
                loggedError = false; page = 0; report = summaryText = ""; examples = []; scroll = new(); measuredHeight = -1;
                session.Check(choice);
                if (session.State != PreviewState.Running) CacheReport();
            }
            GUI.enabled = wasEnabled && session.State == PreviewState.Running;
            if (Widgets.ButtonText(new(162, 168, 150, 32), T("RC_Cancel"))) { session.Cancel(); CacheReport(); }
        }
        finally { GUI.enabled = wasEnabled; }

        string status = session.State == PreviewState.Running ? F("RC_Progress", session.Attempted, session.Snapshot!.Choice.SampleCount) : T("RC_State_" + session.State);
        Widgets.Label(new(324, 172, Math.Max(0, rect.width - 324), 28), status);
        string note = session.Snapshot is not null && !source.ContextMatches() ? T("RC_ContextChanged") :
            session.Snapshot is not null && session.Snapshot.Choice != choice ? T("RC_ConditionsChanged") : T("RC_Limits");
        Widgets.Label(new(0, 212, rect.width, 50), note);

        if (examples.Length > 0)
        {
            if (Widgets.ButtonText(new(0, 268, 90, 28), T("RC_Previous"))) { page = (page + examples.Length - 1) % examples.Length; RefreshPage(); }
            if (Widgets.ButtonText(new(102, 268, 90, 28), T("RC_Next"))) { page = (page + 1) % examples.Length; RefreshPage(); }
            Widgets.Label(new(210, 270, rect.width - 210, 26), F("RC_ExamplePage", page + 1, examples.Length));
        }
        var outer = new Rect(0, 306, rect.width, Math.Max(40, rect.height - 316));
        float width = Math.Max(80, outer.width - 22);
        var displayText = report.Length == 0 ? T("RC_Ready") : report;
        if (measuredHeight < 0 || measuredWidth != width)
        {
            measuredWidth = width;
            measuredHeight = Text.CalcHeight(displayText, width) + 12;
        }
        var view = new Rect(0, 0, width, Math.Max(outer.height, measuredHeight));
        Widgets.BeginScrollView(outer, ref scroll, view);
        Widgets.Label(new(0, 0, width, view.height), displayText);
        Widgets.EndScrollView();
    }

    private void OpenFactions() => Find.WindowStack.Add(new FloatMenu(factions.Select(f => new FloatMenuOption(f.Name, () => choice = choice with { FactionId = f.GetUniqueLoadID() })).ToList()));

    private void OpenStrategies() => Find.WindowStack.Add(new FloatMenu(DefDatabase<RaidStrategyDef>.AllDefsListForReading
        .Where(s => s.defName != "ImmediateAttackFriendly")
        .Select(s => new FloatMenuOption(StrategyName(s.defName) + (WorkerPolicy.SupportsStrategy(s.workerClass) ? "" : " (" + T("RC_NotSupported") + ")"),
            WorkerPolicy.SupportsStrategy(s.workerClass) ? () => { choice = choice with { StrategyId = s.defName }; NormalizeArrival(); } : null)).ToList()));

    private void OpenArrivals()
    {
        var strategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(choice.StrategyId);
        var modes = strategy?.arriveModes ?? [];
        Find.WindowStack.Add(new FloatMenu(modes.Select(a => new FloatMenuOption(ArrivalName(a.defName) + (WorkerPolicy.SupportsArrival(a.workerClass) ? "" : " (" + T("RC_NotSupported") + ")"),
            WorkerPolicy.SupportsArrival(a.workerClass) ? () => choice = choice with { ArrivalId = a.defName } : null)).ToList()));
    }

    private void NormalizeArrival()
    {
        var modes = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(choice.StrategyId)?.arriveModes;
        if (modes?.Any(a => a.defName == choice.ArrivalId && WorkerPolicy.SupportsArrival(a.workerClass)) == true) return;
        choice = choice with { ArrivalId = modes?.FirstOrDefault(a => WorkerPolicy.SupportsArrival(a.workerClass))?.defName ?? "" };
    }

    private void CacheReport()
    {
        var text = new StringBuilder();
        var snap = session.Snapshot;
        if (snap is not null)
        {
            text.AppendLine(F("RC_Captured", snap.MapLabel, snap.GameTicks / 60000d, snap.FactionLabel));
            text.AppendLine(F("RC_CapturedConditions", StrategyName(snap.StrategyLabel), ArrivalName(snap.ArrivalLabel), snap.Choice.SampleCount));
            text.AppendLine(F("RC_Points", snap.BasePoints));
            text.AppendLine(F("RC_Successes", session.Results.Count, snap.Choice.SampleCount));
        }
        foreach (var error in session.Failures.GroupBy(f => f.Code)) text.AppendLine(F("RC_Failure", T(error.Key), error.Count()));
        if (session.Summary is { } summary)
        {
            text.AppendLine(); text.AppendLine(F("RC_Range", summary.Minimum, summary.Median, summary.Maximum));
            text.AppendLine(T("RC_Aggregate"));
            foreach (var kind in summary.Kinds) text.AppendLine(F("RC_KindAverage", kind.Label, kind.DefName, kind.MeanCount, kind.PresentInSamples, session.Results.Count));
        }
        summaryText = text.ToString();
        examples = session.Results.Select((sample, index) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine(F("RC_Example", index + 1, sample.Pawns.Count, sample.AdjustedPoints, sample.Pawns.Sum(p => (double)p.CombatPower)));
            foreach (var group in sample.Pawns.GroupBy(p => p.DefName).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
                sb.AppendLine(F("RC_KindCount", group.First().Label, group.Key, group.Count()));
            return sb.ToString();
        }).ToArray();
        page = 0; RefreshPage();
        LogUnexpectedOnce();
    }

    private void LogUnexpectedOnce()
    {
        if (!loggedError && session.UnexpectedError is { } unexpected)
        {
            Log.Error("[Raid Check] Preview failed: " + unexpected);
            loggedError = true;
        }
    }

    private void RefreshPage() { report = summaryText + (examples.Length > 0 ? "\n" + examples[page] : ""); scroll = new(); measuredHeight = -1; }
    private static string T(string key) => key.Translate().ToString();
    private static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);
    private static string StrategyName(string id) => id switch
    {
        "ImmediateAttack" or "ImmediateAttackSmart" or "StageThenAttack" or "Siege" or "ImmediateAttackSappers" or "ImmediateAttackBreaching" or "ImmediateAttackBreachingSmart" or "EmergeFromWater" => T("RC_Strategy_" + id),
        _ => DefDatabase<RaidStrategyDef>.GetNamedSilentFail(id)?.label ?? id
    };
    private static string ArrivalName(string id) => id switch
    {
        "EdgeWalkIn" or "EdgeWalkInGroups" or "EdgeWalkInDistributed" or "EdgeWalkInDistributedGroups" or "EdgeDrop" or "EdgeDropGroups" or "CenterDrop" or "RandomDrop" or "EmergeFromWater" or "EdgeWalkInDarkness" => T("RC_Arrival_" + id),
        _ => DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail(id)?.label ?? id
    };
}
