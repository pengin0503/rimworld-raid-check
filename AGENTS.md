# Raid Check development

- Target RimWorld 1.7. Calculations run only after explicit Check; never introduce tick hooks, timers or automatic recalculation.
- Keep work on a feature branch; open a PR and do not merge without the user's instruction.
- Support standard alternative strategies and arrivals in v0.1, not only immediate edge walk-in.
- Use the game's kind-only preview and raid point adjustment APIs. Never generate or spawn Pawns, incidents, loot or lords for a preview.
- Isolate random state, fully enumerate inside that scope, restore it in finally, and never keep a scope across frames.
- Game and third-party assets come only from the user's private R2 `rimworld-17/Game-Linux/` and `Workshop/<verified ID>/`, as needed. Reuse matching snapshots. No bulk downloads without a concrete validation need.
- Before choosing a third-party dependency, resolve its name, Workshop ID and PackageId in the user's private MOD index. None is currently required.
- Never commit or package game DLLs/assets, decompiled game bodies, third-party mods, the private index, credentials, personal data or raw private logs.
- Reference builds and test doubles do not prove actual game behavior. Report real-API/UI checks separately and mark unverified scopes honestly.
- Prefer local implementation with one final independent review; avoid per-task subagents.
