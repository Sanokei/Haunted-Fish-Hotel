# Trap behavior source checks

These tests compile the actual production rule classes. They do **not** execute Unity physics, prefab import, Mirage weaving, RPC/SyncVar delivery, or rendering.

With a .NET 9 SDK:

```
dotnet run --project Tools/TrapBehaviorTests/TrapBehaviorTests.csproj
```

With Mono/C# already included in an existing Unity installation (does not launch the Editor):

```
bash Tools/TrapBehaviorTests/RunMono.sh '/path/to/Unity/Editor/Data'
```

Coverage: tablecart nonlethal impact, continuous-contact and multi-collider deduplication, stun/knockback timing, clear/re-entry, global-lock pause, capsule jump clearance, geometric multi-victim traversal, union slowdown for overlapping victims, corpse interval persistence/cleanup, reversing direction, reset, invalid values, and tick-rate invariance.

Run the authored death-effect source/serialization guard separately:

```
python3 Tools/TrapBehaviorTests/verify_death_emergence.py
```

This verifies the disabled nested prefab binding, preserved soul layouts, absence of runtime construction/destruction in the new death presentation path, stable event-position anchoring, and preservation of the piranha binding. It is not a Unity import or animation playback test.

## Runtime ownership

- `TrapImpactRules` contains effect selection, `TrapContactEpisodes<T>` contains one hit per continuous contact, and `TrapStunState` contains authority-owned timed status/impulse integration.
- `TrapPassageMotion` integrates speed through exact horizontal capsule/box contact intervals. `TrapPassageMemory<T>` snapshots those intervals before a lethal effect disables a victim's controller.
- `GhostTrapPassageSweep` adapts Unity casts, authored solid parts and round-scoped fish to those rules. Fish never limit its distance; solid scenery, other traps, the hallway edge and the existing grace-zone travel bounds still do.
- `GhostPlacementWorld` retains authority, movement/snapshot publication and collision ordering. `GameHauntingController` selects the impact and retains the existing damage-to-spectator path. Contacts are committed only after the effect succeeds.
- `HotelPlayer` owns the replicated stun bit. No client-owned stun RPC or local status timer is introduced. `HotelPlayerMovement` owns the authoritative timer; its existing motor applies collision-constrained horizontal displacement while continuing gravity. Hard `CanMove`/shared-dialogue locks pause timer and impulse. A local input-focus/menu lock does not cancel or pause them.
- Death, teleport, role/scene reset, controller authority loss, disable and round-inventory clearing release stun. `HotelFishSprite` presents the replicated status using only its authored effect references.

## Serialized art contracts

Existing prefab names, script GUIDs and family `cart` remain stable. The production cart's display name is `Tablecart`, `_Impact` is `1` (`KnockbackStun`), and default tuning is 1 second stun, 1.2 units knockback over 0.22 seconds. Health is unchanged. The uploaded `TRAP-Hotel-cart.png` is now authored in this same prefab; its 2.5 by 1.64556962 envelope preserves the source art aspect ratio. Three authored solid boxes cover its cloth/body, dome and handle. All 1,071,515 nontransparent source pixels lie in those solid parts. Its supply/inventory icon uses a centered opaque-envelope sprite rectangle, while the in-world layout retains the full source canvas. The older shopping-cart PNG remains available for the disabled click example and historical assets.

`Assets/Resources/Traps/AnglerFishTrap.prefab` now authors the original normal/glowing angler PNGs, solid body parts, disabled possessed glow, disabled lure point light and these settings:

- `_FamilyTag: angler`, `_DisplayName: Angler fish`
- `_InputMode: 0` (Movement), `_Behaviour: 3` (PiercingSlide)
- `_PiercingSpeed: 14`, `_BodySpeedFactor: 0.25`, `_TravelHalfWidth: 6`

`PiercingSlide` always uses lethal contact. It moves only with valid movement input and retains the existing 0.3-second input timeout. Slowdown is applied only across actual body intervals; overlapping bodies share one slowdown factor. Each live fish is struck once per passage, and the cached body span remains until exit even after death. The production `Game.unity` catalog now includes it at weight 2 (tablecart 5, chandelier 3, disabled click example 0). This is source-authored and still needs native import/playback validation.

`GhostTrapAreaPresentation` has optional authored `_PossessedLight` (`Light`) and `_PossessedGlow` (`GameObject`) references. Their state follows a ready ghost possessing this exact trap in the same room/round. Neither is constructed at runtime. Angler enables `_PossessedGlowReplacesBody`: its glow PNG is a full alternate fish, so only the normal or possessed image draws at a time (avoiding doubled antialiased edges).

The chandelier now uses supplied v3 art; v1/v2 are also imported for art-direction choice. Each unchanged 2048-square PNG has three Unity sprite rectangles: complete icon, canopy at source top rows 0–259, and suspended cord/body at rows 260–2047. The black hand-drawn cord is present and preserved. `_SuspendedPivot` binds a UI layout pivot at source (1024,260); its suspended Image is projected through the existing world-art pass, so the projector retains the sway. `_FixedCeilingLayout` is a separate authored sibling projected from `Origin + ArmedOffset`, keeping the ceiling canopy stationary during a drop. The combined Image supplies dimensions only and is disabled; it is excluded from `_WorldImages`, avoiding duplicate artwork. Full opaque dimensions are 1.89584699 by 1.9, preserving aspect ratio and leaving 2.21 units of floor clearance below the armed trap in the 4.11-unit hallway. The pivot returns to its authored resting orientation on falling, release, reset and disable. Sway remains 3 degrees at 2.2 radians/second.

`HotelFishSprite` binds `_StunEffect` to a disabled authored piranha effect root and `_StunAnimation` to its looping legacy `Animation`. Entry activates/plays once; exit/death/disable stops and hides it. The production network-player prefab has these bindings. No new runtime objects, controllers, materials or UI are built for the effect.

`HotelPlayer._DeathEmergence` binds the disabled nested `Assets/Prefabs/Effects/DeathEmergence.prefab`. Its `AuthoredGhostEmergence` driver reuses the original soul sprite and appear/rise/clear Ink layouts, then hides the same objects. It rejects story operations that create UI. Because this canvas is nested, it uses Screen Space Camera with an explicit view-camera binding and canvas-plane projection; it stays hidden without a valid camera. Its anchor follows the replicated death location, independent of later actor movement. The death-version observer does not replay historical deaths on first observation and stops effects on player/round teardown. The floor corpse remains a spectator; the original ghost role is unchanged. The old standalone `Resources/GhostEmergence.prefab` is preserved rather than changing its existing script identity.

## Artwork source checks

```
python3 Tools/TrapBehaviorTests/verify_trap_artwork.py
python3 Tools/PlayerTests/VerifyCartArtwork.py
```

These inspect all prefab local references and hierarchy relationships, six original SHA-256 hashes, import settings, silhouette coverage, catalog integration, glow/light bindings, split sprite rectangles, top-of-cord rotation geometry and fixed-canopy/reset source contracts. `TrapArtworkSources.json` records provenance. Pillow reads pixels only; no raster asset was edited. The authored reference preview is explicitly labelled as a source-layout preview, not a Unity capture.

Historical native jump screenshots/evidence are tied to the previous shopping-cart silhouette. `VerifyNativeCartEvidence.py` must report them stale until native checks are rerun; do not update its recorded hashes to make old evidence pass.

## Still required in native Unity

Run actual project import/compilation and Mirage weaving, then verify with two clients:

1. Hit a standing fish with tablecart from both directions. Health stays unchanged; displacement stops at solids and hallway bounds; run/jump is suppressed for the stun and resumes. Remote peers see the same effect once.
2. Pin a fish to a wall and maintain contact for several seconds. The tablecart does not kill it or restart stun every frame. Separate/re-enter for a new strike. Repeat during post-teleport settle frames.
3. Open a local menu during stun: authority timing still expires. Use a hard movement/shared-dialogue lock: impulse/status pause and resume without moving through the lock. Interrupt with death, teleport, boss transition, scene change and disconnect: no leftover effect or delayed impulse.
4. With a genuinely authored angler, cross two or more fish in one step and several across consecutive steps, both directions. Every crossed fish becomes a spectator; the angler slows while traversing each cached body, then resumes. Cluster victims and jump over/round its solid parts. Check foreground walls, other traps, grace area, release/input timeout and room/round isolation.
5. Confirm authored possession light is dark when unpossessed and visible to both peers when possessed; confirm the chandelier ceiling base stays fixed while only its separated suspended art sways around the line pivot.
6. Kill a fish: the authored ghost image rises through its floor corpse and clears, with no object creation or role swap. Repeat/reset/interruption reuses the same disabled child. A late observer does not replay a historical death; camera or retained-actor motion does not move the original event anchor. Test two simultaneous victims and player/round disable.

The cloud Editor cannot currently start due to socket/IPC restrictions. Successful source checks must not be reported as native physics, animation, network delivery, visual or full-project compile validation.
