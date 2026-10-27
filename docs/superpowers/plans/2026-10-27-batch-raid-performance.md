# Batch Raid Comparison Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans; implement and verify tasks sequentially.

**Goal:** Resolve issues #2 and #3 and compare multiple factions on demand.
**Architecture:** A batch controller runs independent PreviewSession instances sequentially; UI rebuilds selections on map/game replacement. Reports prepare only visible examples and cache on demand.
**Tech Stack:** C# .NET 10, RimWorld 1.7 reference APIs, existing console tests.
**Spec:** docs/superpowers/specs/2026-10-27-batch-raid-performance-design.md

## Global Constraints
- No periodic raid calculations; no parallel game calls; RNG scope never crosses a frame.
- Preserve single-faction flow, failure reasons and cancellation.
- Do not merge without explicit instruction; real game validation is distinct from stubs.

## Review Focus
- Duplicate faction IDs and zero selection; invalid choices cannot silently run.
- Failed captures and samples must not prevent subsequent factions.
- Map/game change cancels batch and discards stale selection and results.
- Close/cancel cannot start a queued faction or sample.
- Large result sets must not eagerly build every page or duplicate all pawn data.

### Task 1: Batch session
Files: Source/Core/BatchPreviewSession.cs, Tests/BatchTests.cs, Tests/Program.cs.
- [ ] Write failing tests for ordered two-faction progress, capture failure followed by success, cancellation, duplicate choices and idle no calls.
- [ ] Implement batch state and one sample per advance, with isolated source per faction.
- [ ] Run complete console suite and commit.

### Task 2: Context and report efficiency
Files: Source/UI/MainTabWindow_RaidCheck.cs, Source/Core/Models.cs, Tests/UiTests.cs.
- [ ] Add failing tests for context change when idle, active and completed, stale menu actions, and no eager page construction.
- [ ] Refresh faction list and reset state on map/game identity change; construct only requested sample page and cache it.
- [ ] Measure large synthetic result timing and memory; run console suite and commit.

### Task 3: UI comparison and packaging
Files: Source/UI/MainTabWindow_RaidCheck.cs, Mod/Languages/*/Keyed/RaidCheck.xml, README.md, Tests/UiTests.cs.
- [ ] Add failing UI tests for multiple selection, comparison, failed faction, cancel and single selection.
- [ ] Add faction selection, aggregate comparison and drilldown; preserve Japanese and English key parity.
- [ ] Run console tests, packaging tests, reference build, ZIP validation and CI; review branch; open PR referencing #2 and #3 without merging.
