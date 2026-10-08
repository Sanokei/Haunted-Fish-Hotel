# Trap supply regression checks

Run from the repository root:

```powershell
dotnet run --project Tools/TrapSupplyTests/TrapSupplyTests.csproj
```

This executable compiles the production `ConveyorSchedule` and `WeightedSelection` directly,
without Unity or package dependencies. It checks release/manual/periodic policies, start/gap
boundaries, pause/resume timing, stop suppression, seed-once behavior, cadence edits,
overdue spawn offsets, the 64-attempt backlog limit, reset and invalid values, and weighted
family selection.

`TrapManager` continues to own scene objects, catalog injection, authority checks, scoped
snapshot validation, pickup, monotonic package IDs/revisions, rendering and disable cleanup.
Those integration behaviors are covered by the existing Unity validation in
`Tools/RoundIntroductionTests/ValidateTrapManager.cs`; run that suite in Unity before release.
