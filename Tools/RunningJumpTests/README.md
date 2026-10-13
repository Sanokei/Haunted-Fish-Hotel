# Intentional running-jump reach

The production game-fish prefab now authors `GameSideScrollMotor._RunningJumpSpeed` at **7.5 units/second**. The new field defaults to **0** in older/unconfigured prefabs, retaining their previous movement behavior. Public `RunningJumpSpeed` exposes the authored value for recording/inspection without overriding it.

Hold **run + a horizontal direction**, then press **jump while grounded**. Continue holding run and that direction through the jump. The speed boost follows analog magnitude: half-stick input gives half the configured speed. Releasing run/direction or reversing direction cancels the boost for that jump; ordinary air steering remains available. Only a new actual grounded jump can re-arm it.

Ground walking stays **1.8**, ground running **4.5**, and lobby walking **6**. The production vertical jump speed remains **10.48**, gravity **25**, and the analytical rise remains approximately **2.1966 m**. This change adds horizontal reach without raising the jump into the existing hallway ceiling.

`RunningJumpState` is a deterministic horizontal-state helper owned by the existing `GameSideScrollMotor`. The motor retains real grounding, authority/input readiness, collision movement, gravity and jump acceptance. A jump command records its run/direction intent; later packets cannot rewrite an earlier walking or directionless jump into a boosted launch. Airborne commands and simply walking off a ledge never grant the boost.

Landing, reversal/release, stale input, stun, hard movement/shared-dialogue locks, failed movement and motion resets cancel it. Teleport, death, scene transitions and authority loss already flow through those resets. Locked inputs cannot queue a launch for after unlock. There is no new global Update controller or client-authoritative boost RPC.

## Executable source checks

With an installed .NET 9 SDK:

```
dotnet run --project Tools/RunningJumpTests/RunningJumpTests.csproj
```

Or use the Mono compiler bundled with an existing Unity installation without starting the Editor:

```
bash Tools/RunningJumpTests/RunMono.sh '/path/to/Unity/Editor/Data'
```

These tests compile the actual production `RunningJumpState` and `GameSideScrollMotor`. Lightweight adapter doubles model a floor/ceiling boundary and input/authority flags. Coverage includes 30/60/120/240 Hz trajectories, unchanged vertical integration, ground/default behavior, analog magnitude, both directions, queued-input ordering, release/reversal, airborne commands, pause/locks/stun, authority rejection, landing and repeated grounded jumps.

The doubles are **not native CharacterController physics**. The source tests do not establish tablecart collision clearance, Unity prefab import, animation/rendering, or real-client delivery. Native CharacterController runs and the authored guide recording are still required. In particular, verify standing-cart clearance in both directions at 30/60/120 Hz, including the actual compound colliders, capsule skin and damage margin; retain the existing ceiling height and test release/reversal, stun and a second jump after landing.
