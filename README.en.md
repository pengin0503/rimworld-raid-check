# Raid Check

[日本語](README.md) | **English**

A RimWorld 1.7 mod that samples possible raid compositions by faction using current raid points. Harmony is not required. **The real game API and interface have not been verified in-game.**

## Installation and use

Place the `RaidCheck` folder from the distribution ZIP in `RimWorld/Mods`, enable the mod and restart. Load a map, open **Raid Check** from the bottom bar, select factions, strategy, arrival method and sample count, and press **Check**. Choose 10, 25 or 50 samples and compare multiple factions. Invalid combinations show a reason.

Results show observed minimum, median and maximum counts, average counts and appearances by enemy type, and example compositions. After completion or cancellation, export them to CSV. The output is in `RaidCheckExports` under RimWorld's user data folder.

## Scope

Calculations run only when **Check** is pressed and stop on cancellation, closing the tab or changing maps. Results are a snapshot and do not update automatically as wealth changes. Real raids may differ because of later generation and other mods.

Custom special generators, mech clusters, infestations, quest and Anomaly special forces, siege supplies and actual spawn locations are out of scope. Even for standard raids, not every faction, strategy and arrival method can be combined.
