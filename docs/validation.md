# Validation scope — Raid Check 0.1.0 preview

## Target and asset evidence

- Target: RimWorld 1.7.4985 rev1269; read from the user's authorized private R2 `Game-Linux/Version.txt`.
- Read only the Core raid-strategy XML definitions and arrival definitions needed to identify supported modes. No game or third-party assets are committed or packaged.
- Compiled against the cached `Krafs.Rimworld.Ref 1.7.4985-unstable` signature-only references.
- The current Cloudflare object connector decodes object bodies as UTF-8. A binary-DLL read confirmed replacement characters, so that output was not written or used as a game DLL. No alternate game source was substituted.

## Automated verification

Local final result: 26 C# tests and 7 Python tests passed; Release reference build succeeded with 0 warnings and 0 errors. Install ZIP integrity and the 8-file runtime allowlist were checked. A read-only whole-branch review found no Critical or concrete Important source defect. Its close-before-completion diagnostic finding was reproduced and fixed with a RED→GREEN lifecycle test. Native validation remains incomplete as detailed below.

- Dependency-free C# console suite runs the actual Core, GamePreviewSource, WorkerPolicy and MainTabWindow_RaidCheck source with narrow game/UI contract doubles.
- Tests cover idle zero point/sample calls, capture-on-Check, single-step progress, no duplicate start, cancel/close/context changes, stale condition display, cached layout measurement, Def identity, medians, partial failures, invalid input, entry limits and exception-time random scope restoration.
- Adapter tests use a sentinel adjusted-point value to detect missed/double adjustments and assert selected strategy and arrival are forwarded to the kind-preview route. This tests our call contract, **not the game's implementation of those APIs**.
- Python tests execute the packaging code with synthetic assembly bytes and check install root, allowlist, missing/corrupt input rejection, translation parity, metadata identity, symlinks and reproducibility.
- Release compilation checks our API usage against the 1.7 reference shapes.

## Unverified scopes

- The native game algorithms have not been executed here. Actual pirate/tribal/mechanoid previews at low/medium/high points, native RNG-sequence preservation and saved-state invariance remain unverified.
- The real in-game UI has not been rendered or clicked; layout, pause behavior and MainButtonDef loading remain unverified in the game.
- Compatibility with arbitrary patched game APIs, custom workers and long-running third-party callbacks is not certified.
- This work does not extend or qualify the separate RimWorld validation host.

The preview ZIP and passing reference/contract checks must not be described as a passed in-game test or a fully validated stable release.

## Batch comparison performance check

A synthetic in-process UI harness generated 25 samples of 10,000 identical pawn kinds and measured the last `WindowUpdate` once on the same machine. Baseline: 56.26 ms and 7,543,840 thread-allocated bytes. After lazy example construction, incremental aggregation and compressed kind storage: 17.72 ms and 677,936 bytes. These single runs are illustrative, not a statistical benchmark or an in-game measurement. Different numbers of distinct pawn kinds and modded workers can behave differently. Each game's native preview API call is still non-preemptible.

The batch changes were checked using C# contract and UI lifecycle tests, package tests and a signature-only reference build. Actual game API behavior, game state invariance, UI rendering and in-game performance remain unverified.
