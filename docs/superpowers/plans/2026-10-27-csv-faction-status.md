# CSV Export and Faction Status Implementation Plan

**Goal:** Correct cancelled and selected faction states and export completed or partial comparisons to CSV on demand.
**Architecture:** Preserve selected batch choices, distinguish not-started/cancelled/failed/completed, and serialize captured data through a game-independent writer. The UI writes one UTF-8 CSV under the RimWorld user data folder only upon an explicit button click.
**Constraints:** No periodic calculations, no external dependencies, no game files included in artifacts. Export preserves source conditions and partial-result status, escapes CSV correctly, protects untrusted text against spreadsheet formulas, and reports errors. User requests implementation and PR, without merge.

- [ ] Add failing lifecycle tests for cancellation before a sample, pending factions, and switching a multiple selection to a different single faction; implement and run full suite.
- [ ] Add failing serializer tests for multi-faction data, metadata, partial/failure states, CSV quoting, UTF-8 and formula-like text; implement and run tests.
- [ ] Add explicit export button with safe unique-file creation, error handling, Japanese/English text and UI tests. Verify no calculations during export.
- [ ] Run suite, reference build, packaging, review, and CI; open PR referencing #5 and #6.
