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
    private string?[] examples = [];
    private int page;
    private readonly HashSet<PreviewSession> loggedErrors = [];
    private float measuredWidth = -1, measuredHeight = -1;
    private Verse.Game? openedGame;
    private Map? openedMap;
    private readonly HashSet<string> selectedFactions = new(StringComparer.Ordinal);
    private readonly HashSet<string> checkedFactions = new(StringComparer.Ordinal);
    private BatchPreviewSession? batch;
    private PreviewChoice? checkedChoice;
    private string exportNotice = "";
    private bool IsRunning => batch?.State == PreviewState.Running || session.State == PreviewState.Running;

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
        ResetForCurrentMap();
    }

    private void ResetForCurrentMap()
    {
        batch?.Cancel(); session.Cancel(); LogUnexpectedOnce(); batch = null;
        openedGame = Current.Game; openedMap = Find.CurrentMap;
        source = new(); session = new(source);
        report = summaryText = exportNotice = ""; checkedChoice = null; examples = []; page = 0; scroll = new(); loggedErrors.Clear(); checkedFactions.Clear(); measuredHeight = -1;
        factions = Current.Game is null || Find.CurrentMap is null ? [] : Find.FactionManager.AllFactionsListForReading
            .Where(f => !f.IsPlayer && !f.defeated && !f.deactivated && f.HostileTo(Faction.OfPlayer) &&
                f.def.pawnGroupMakers?.Any(g => g.kindDef == PawnGroupKindDefOf.Combat) == true)
            .OrderBy(f => f.Name, StringComparer.CurrentCulture).ToList();
        choice = new(factions.FirstOrDefault()?.GetUniqueLoadID() ?? "", "ImmediateAttack", "EdgeWalkIn", 25);
        selectedFactions.Clear();
        if (choice.FactionId.Length != 0) selectedFactions.Add(choice.FactionId);
        if (DefDatabase<RaidStrategyDef>.GetNamedSilentFail(choice.StrategyId) is null)
            choice = choice with { StrategyId = DefDatabase<RaidStrategyDef>.AllDefsListForReading.FirstOrDefault(s => WorkerPolicy.SupportsStrategy(s.workerClass))?.defName ?? "" };
        NormalizeArrival();
    }

    public override void PreClose()
    {
        batch?.Cancel(); session.Cancel();
        LogUnexpectedOnce();
        base.PreClose();
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();
        if (!ReferenceEquals(openedGame, Current.Game) || !ReferenceEquals(openedMap, Find.CurrentMap))
        {
            ResetForCurrentMap();
            return;
        }
        if (batch?.State == PreviewState.Running)
        {
            batch.Advance(true);
            if (batch.State != PreviewState.Running)
            {
                session = batch.Items.FirstOrDefault(i => i.Session.Summary is not null)?.Session ?? batch.Items[^1].Session;
                CacheReport();
            }
            return;
        }
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
            GUI.enabled = wasEnabled && !IsRunning;
            string factionLabel = selectedFactions.Count > 1 ? F("RC_SelectedCount", selectedFactions.Count) : factions.FirstOrDefault(f => selectedFactions.Contains(f.GetUniqueLoadID()))?.Name ?? T("RC_None");
            if (Widgets.ButtonText(new(0, 84, half, 32), T("RC_Faction") + ": " + factionLabel)) OpenFactions();
            if (Widgets.ButtonText(new(half + 12, 84, half, 32), T("RC_Strategy") + ": " + StrategyName(choice.StrategyId))) OpenStrategies();
            if (Widgets.ButtonText(new(0, 124, half, 32), T("RC_Arrival") + ": " + ArrivalName(choice.ArrivalId))) OpenArrivals();
            if (Widgets.ButtonText(new(half + 12, 124, half, 32), T("RC_Samples") + ": " + choice.SampleCount))
                Find.WindowStack.Add(new FloatMenu(new[] {10,25,50}.Select(n => new FloatMenuOption(n.ToString(), () => choice = choice with { SampleCount = n })).ToList()));
            if (Widgets.ButtonText(new(rect.width - 180, 168, 180, 32), T("RC_SelectMultiple"))) OpenBatchFactions();
            GUI.enabled = wasEnabled && !IsRunning && selectedFactions.Count != 0 && Find.CurrentMap is not null;
            if (Widgets.ButtonText(new(0, 168, 150, 32), T("RC_Check")))
            {
                loggedErrors.Clear(); checkedFactions.Clear(); checkedFactions.UnionWith(selectedFactions); checkedChoice = choice with { FactionId = selectedFactions.First() }; exportNotice = ""; page = 0; report = summaryText = ""; examples = []; scroll = new(); measuredHeight = -1;
                batch = null;
                if (selectedFactions.Count == 1)
                {
                    source = new(); session = new(source); session.Check(choice with { FactionId = selectedFactions.First() });
                }
                else
                {
                    batch = new BatchPreviewSession(() => { source = new(); return source; });
                    if (batch.Check(factions.Where(f => selectedFactions.Contains(f.GetUniqueLoadID())).Select(f => choice with { FactionId = f.GetUniqueLoadID() }).ToArray()))
                        session = batch.Items[0].Session;
                    else batch = null;
                }
                if (!IsRunning) CacheReport();
            }
            GUI.enabled = wasEnabled && IsRunning;
            if (Widgets.ButtonText(new(162, 168, 150, 32), T("RC_Cancel"))) { batch?.Cancel(); session.Cancel(); CacheReport(); }
        }
        finally { GUI.enabled = wasEnabled; }

        string status = batch?.State == PreviewState.Running ? F("RC_BatchProgress", batch.Items.Count, selectedFactions.Count) : session.State == PreviewState.Running ? F("RC_Progress", session.Attempted, session.Snapshot!.Choice.SampleCount) : T("RC_State_" + (batch?.State ?? session.State));
        Widgets.Label(new(324, 172, Math.Max(0, rect.width - 510), 28), status);
        string note = session.Snapshot is not null && (!ReferenceEquals(openedGame, Current.Game) || !ReferenceEquals(openedMap, Find.CurrentMap)) ? T("RC_ContextChanged") :
            session.Snapshot is not null && (!checkedFactions.SetEquals(selectedFactions) || session.Snapshot.Choice.StrategyId != choice.StrategyId || session.Snapshot.Choice.ArrivalId != choice.ArrivalId || session.Snapshot.Choice.SampleCount != choice.SampleCount) ? T("RC_ConditionsChanged") : T("RC_Limits");
        Widgets.Label(new(0, 212, rect.width, 50), note);

        if (examples.Length > 0)
        {
            if (Widgets.ButtonText(new(0, 268, 90, 28), T("RC_Previous"))) { page = (page + examples.Length - 1) % examples.Length; RefreshPage(); }
            if (Widgets.ButtonText(new(102, 268, 90, 28), T("RC_Next"))) { page = (page + 1) % examples.Length; RefreshPage(); }
            Widgets.Label(new(210, 270, rect.width - 210, 26), F("RC_ExamplePage", page + 1, examples.Length));
        }
        if (batch is not null && !IsRunning && batch.Items.Count > 1 && Widgets.ButtonText(new(rect.width - 180, 268, 180, 28), T("RC_ViewFaction")))
            Find.WindowStack.Add(new FloatMenu(batch.Items.Select(i => new FloatMenuOption(i.Session.Snapshot?.FactionLabel ?? factions.FirstOrDefault(f => f.GetUniqueLoadID() == i.Choice.FactionId)?.Name ?? i.Choice.FactionId, () => { session = i.Session; CacheReport(); })).ToList()));
        bool oldEnabled = GUI.enabled;
        try
        {
            GUI.enabled = oldEnabled && !IsRunning && checkedChoice is not null;
            if (Widgets.ButtonText(new(rect.width - 380, 268, 190, 28), T("RC_ExportCsv"))) ExportCsv();
        }
        finally { GUI.enabled = oldEnabled; }
        if (Widgets.ButtonText(new(rect.width - 190, 306, 190, 28), T("RC_OpenExportFolder"))) OpenExportFolder();
        var outer = new Rect(0, 344, rect.width, Math.Max(40, rect.height - 354));
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

    private Action IfCurrent(Action action)
    {
        var game = openedGame; var map = openedMap;
        return () => { if (ReferenceEquals(game, Current.Game) && ReferenceEquals(map, Find.CurrentMap) && ReferenceEquals(game, openedGame) && ReferenceEquals(map, openedMap)) action(); };
    }
    private void OpenFactions() => Find.WindowStack.Add(new FloatMenu(factions.Select(f => new FloatMenuOption(f.Name, IfCurrent(() => { choice = choice with { FactionId = f.GetUniqueLoadID() }; selectedFactions.Clear(); selectedFactions.Add(choice.FactionId); }))).ToList()));
    private void OpenBatchFactions() => Find.WindowStack.Add(new FloatMenu(factions.Select(f => new FloatMenuOption((selectedFactions.Contains(f.GetUniqueLoadID()) ? "✓ " : "☐ ") + f.Name, IfCurrent(() => { var id = f.GetUniqueLoadID(); if (!selectedFactions.Add(id)) selectedFactions.Remove(id); }))).ToList()));

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
        if (batch is not null)
        {
            text.AppendLine(T("RC_Comparison"));
            foreach (var selected in batch.Choices)
            {
                var item = batch.Items.FirstOrDefault(i => i.Choice.FactionId == selected.FactionId);
                string name = item?.Session.Snapshot?.FactionLabel ?? factions.FirstOrDefault(f => f.GetUniqueLoadID() == selected.FactionId)?.Name ?? selected.FactionId;
                string reason = item is null ? T("RC_NotStarted") : item.Session.State == PreviewState.Cancelled ? T("RC_State_Cancelled") : T(item.Session.Failures.FirstOrDefault()?.Code ?? "RC_Error_Empty");
                text.AppendLine(item?.Session.Summary is { } s ? F("RC_ComparisonLine", name, s.Minimum, s.Median, s.Maximum) + (item.Session.State == PreviewState.Cancelled ? " (" + T("RC_State_Cancelled") + ")" : "") : F("RC_ComparisonFailed", name, reason));
            }
            text.AppendLine();
        }
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
        examples = new string?[session.Results.Count];
        page = 0; RefreshPage();
        LogUnexpectedOnce();
    }

    private void LogUnexpectedOnce()
    {
        var sessions = batch is null ? new[] { session } : batch.Items.Select(item => item.Session);
        foreach (var item in sessions)
        {
            if (item.UnexpectedError is not { } unexpected || !loggedErrors.Add(item)) continue;
            Log.Error("[Raid Check] Preview failed: " + unexpected);
        }
    }

    private void RefreshPage()
    {
        if (examples.Length > 0 && examples[page] is null)
        {
            var sample = session.Results[page]; var sb = new StringBuilder();
            sb.AppendLine(F("RC_Example", page + 1, sample.Pawns.Count, sample.AdjustedPoints, sample.Pawns.Sum(p => (double)p.CombatPower)));
            foreach (var group in sample.Pawns.GroupBy(p => p.DefName).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
                sb.AppendLine(F("RC_KindCount", group.First().Label, group.Key, group.Count()));
            examples[page] = sb.ToString();
        }
        report = (exportNotice.Length > 0 ? exportNotice + "\n\n" : "") + summaryText + (examples.Length > 0 ? "\n" + examples[page] : ""); scroll = new(); measuredHeight = -1;
    }
    private void ExportCsv()
    {
        try
        {
            IReadOnlyList<CsvFaction> records;
            if (batch is null)
            {
                var c = session.Snapshot?.Choice ?? checkedChoice!;
                records = [new(c, session.Snapshot?.FactionLabel ?? factions.FirstOrDefault(f => f.GetUniqueLoadID() == c.FactionId)?.Name ?? c.FactionId, session)];
            }
            else records = batch.Choices.Select(c =>
            {
                var item = batch.Items.FirstOrDefault(i => i.Choice.FactionId == c.FactionId);
                return new CsvFaction(c, item?.Session.Snapshot?.FactionLabel ?? factions.FirstOrDefault(f => f.GetUniqueLoadID() == c.FactionId)?.Name ?? c.FactionId, item?.Session);
            }).ToArray();
            var path = RaidCsv.WriteFile(ExportFolder, records);
            exportNotice = F("RC_ExportSaved", path);
        }
        catch (Exception ex)
        {
            Log.Error("[Raid Check] CSV export failed: " + ex);
            exportNotice = F("RC_ExportFailed", ex.Message);
        }
        RefreshPage();
    }
    private static string ExportFolder => Path.Combine(GenFilePaths.SaveDataFolderPath, "RaidCheckExports");
    private void OpenExportFolder()
    {
        try
        {
            var folder = Path.GetFullPath(ExportFolder);
            Directory.CreateDirectory(folder);
            Application.OpenURL(new Uri(folder + Path.DirectorySeparatorChar).AbsoluteUri);
        }
        catch (Exception ex)
        {
            Log.Error("[Raid Check] Opening CSV folder failed: " + ex);
            exportNotice = F("RC_OpenExportFolderFailed", ex.Message);
            RefreshPage();
        }
    }
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
