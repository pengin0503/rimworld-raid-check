# Raid Check Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Ship an on-demand RimWorld 1.7 enemy-composition preview including alternative raid strategies and arrivals.

**Architecture:** A game-independent session advances one bounded sample per open-window update after Check only. A game adapter captures current points and uses native point adjustment, option validation and kind-only previews inside isolated random scopes. A standard main tab displays captured results without background monitoring.

**Tech Stack:** C# / .NET 10; Krafs.Rimworld.Ref 1.7.4985-unstable (compile only); dependency-free console regression harness; Python package validation; GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-27-raid-composition-preview-design.md`

## Global Constraints

- Calculations run only on Check; no tick hooks, timers, automatic refresh or Harmony dependency.
- Samples: 10, 25 or 50; default 25. One sample per WindowUpdate; maximum 10,000 entries per sample, enforced while enumerating.
- Pirates, tribals and mechanoids; immediate, smart, stage, sapper, breach, smart breach and siege strategies; compatible native arrivals including drops.
- No Pawn creation, spawn-center resolution, raid execution, lords, loot or save components.
- All random-consuming reads and lazy enumeration are enclosed in a per-call Push/Pop finally scope.
- No private/game/third-party files in commits, CI artifacts or install ZIPs.
- Work in this fresh dedicated checkout on `feat/on-demand-raid-composition`; never merge without instruction.

## Review Focus

- Different strategy/arrival choices must reach the game's adjustment and kind selection unchanged; no plausible-looking generic fallback.
- Invalid faction/strategy/arrival combinations must fail explicitly instead of returning zero enemies.
- Closing or changing map/game must prevent future samples and stale results being represented as current.
- Lazy enumeration failure or malformed enemy costs must not leak random state or freeze subsequent frames.
- Identical translated kind labels must stay distinct by Def name; failed samples must not bias statistics toward zero.

## Files and responsibilities

- `Source/Core/Models.cs`: immutable choices, captured metadata, sample and summary values.
- `Source/Core/PreviewSession.cs`: explicit start, bounded advance, cancellation and aggregation.
- `Source/Core/RandomIsolation.cs`: synchronous seeded finally scope.
- `Source/Game/GamePreviewSource.cs`: native capture, compatibility checks, adjusted points and kind-only preview.
- `Source/Game/WorkerPolicy.cs`: exact standard worker allowlist and non-generating policy.
- `Source/UI/MainTabWindow_RaidCheck.cs`: controls, one active session, captured result rendering.
- `Tests/`: real core and adapter code with explicit narrow game doubles; never claim these execute the game.
- `Mod/`: About, MainButtonDef and English/Japanese keyed text.
- `tools/package.py`: validated install ZIP containing only self-authored runtime files.

### Task 1: Session and statistics

**Files:** `Source/Core/*.cs`, `Tests/CoreTests.cs`, `Tests/RaidCheck.Tests.csproj`.

**Interfaces:** Produces `IPreviewSource.Capture(PreviewChoice): PreviewSnapshot`, `Sample(PreviewSnapshot,int): SampleResult`; `PreviewSession.Check(PreviewChoice): bool`, `Advance(bool): void`, `Cancel(): void`; `RandomIsolation.Run<T>(IRandomState,int,Func<T>): T`.

- [x] Write console regression cases for idle zero calls, Check capture once, duplicate start rejection, one sample per step, complete/partial/all failures, cancellation/context changes, median and duplicate labels, finite positive costs, bounded enumeration and finally restoration including lazy exceptions.
- [x] Run `dotnet run --project Tests -c Release`; observe missing-feature failure.
- [x] Implement the core, keeping immutable captured choices and completion-time summaries.
- [x] Run the full console suite; expect all named checks PASS. Commit.

### Task 2: Native game adapter

**Files:** `Source/Game/*.cs`, `Tests/GameStubs.cs`, `Tests/AdapterTests.cs`.

**Interfaces:** Consumes Task 1 types; produces `GamePreviewSource` implementing `IPreviewSource` and `ContextMatches()`, `WorkerPolicy` for exact native worker types.

- [x] Add failing adapter tests: Check-only point reads; all supported strategies/arrivals forwarded unchanged; adjusted points passed once to default group parms; generated entries use native Def identity; incompatible/no-Combat/custom workers refused; lazy exception restores random state; map/game changes invalidate context.
- [x] Run console tests; observe the adapter cases fail before implementation.
- [x] Implement using `DefaultThreatPointsNow`, `AdjustedRaidPoints`, `GetDefaultPawnGroupMakerParms`, `GeneratePawnKindsExample`. Validate supported native workers before invoking callbacks. Never call Pawn generation/arrival/spawn APIs.
- [x] Run all console checks and compile adapter against cached 1.7 reference package. Record that real-DLL behavior is not tested. Commit.

### Task 3: Manual-check UI and mod metadata

**Files:** `Source/UI/MainTabWindow_RaidCheck.cs`, `Source/RaidCheck.csproj`, `Mod/**`.

**Interfaces:** Consumes session and game adapter. MainButtonDef resolves `RaidCheck.MainTabWindow_RaidCheck`.

- [x] Extend a narrow UI harness to expose window lifecycle and invoke real window methods; assert opening/rendering idle does not capture points/sample, only clicking Check starts, close/context change cancels, finished rendering doesn't recalculate, and changing controls leaves captured metadata distinct.
- [x] Run and observe missing-UI failures.
- [x] Implement standard main tab, selection menus, 10/25/50 sample choices, Check/Cancel, progress, captured conditions, summary, aggregate kinds and pageable examples. Use scrolling and stable captured display text. Menus only read definitions until Check.
- [x] Add English/Japanese keyed text and MainButtonDef; target net10.0, no runtime game/reference dependencies copied into output.
- [x] Run full regression suite and Release reference build; validate XML and localized keys. Commit.

### Task 4: Package, CI and review

**Files:** `tools/package.py`, `Tests/test_package.py`, `.github/workflows/ci.yml`, `README.md`, `docs/validation.md`.

**Interfaces:** Consumes compiled self-authored DLL; produces `dist/RaidCheck-0.1.0-RW1.7.zip` with single `RaidCheck/` root.

- [x] Write package tests using synthetic DLL inputs: expected structure, reject missing DLL, no build/source/private files; run RED.
- [x] Implement explicit packaging allowlist and XML/key checks. README lists actual supported modes, usage, statistical limits, no background calculation, incomplete landing/individual-equipment checks and real-game validation gap.
- [x] Run console suite, Python tests, Release build and package validation. Commit and make one independent whole-branch review; fix material findings with regression cases.
- [ ] Push feature branch, open PR, inspect CI outcome, and link PR plus install ZIP. No merge or formal stable release without instruction.

## Pre-flight self-review

Tasks 2–3 consume the exact Task 1 interfaces. Task 3 output path is the Task 4 input path. User chose repository work plus expanded first-version scope; this is an execution instruction. Implement locally without further process-only approval prompts; retain one final review. The design and this plan travel in the repository. Real game binary acquisition is a separate verification limitation, not a reason to substitute another game source.
