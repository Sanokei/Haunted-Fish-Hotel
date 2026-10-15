# Hot-potato cue bomb and twelve-piece defenses

**Rules (pure C#, tested):** `BossDefenseCatalog`, `BossFuse`, `BossBlast`, `BossStunState`, `BossDefenseState.ApplyBlast`.
`dotnet run --project Tools/BossHotPotatoTests/BossHotPotatoTests.csproj` (16k checks). `Tools/BossHotPotatoTests/UnityCompileCheck`
type-checks the real Unity-facing scripts against stand-ins (not a substitute for the editor).

**Game rules.** The cue ball is a lit bomb (9 s fuse, burns 0.6 s faster per paddle strike, never below 1.5 s). Whoever is
on the half where it goes off is stunned 1.6 s, and every standing piece within 1.9 m on either side takes a hit. A struck
dynamite crate burns 2.5 s off the fuse. The next serve lights a new one. The boss juggles, panics and flinches along.

**New pieces (per side):** bumper (3 hp, bouncier than anything else), ramp (2 hp, angled deflector), dynamite crate (1 hp).
Slots 0-5 are unchanged, 6-8 fish new pieces, 9-11 ghost new pieces, so older arena copies with six pieces still bind.

**Authoring scripts** (run once, they refuse to repeat): `author_bomb.py` (fuse/blast/stun visuals, controllers, scene wiring),
`author_pieces.py` (pieces, second tray column, board references, wider camera frame). `../validate_yaml_refs.py` checks references.
`layout-plan.png` is a plan view drawn from the authored coordinates.

**Everything here is unverified in the Unity editor.** Import, open `BossFight.unity`, and check: the bomb component binds,
fuse/blast/stun animators play, the new pieces drag from the outer trays, the camera frame looks right in landscape and portrait,
and the Ramp collider angle deflects the cue. Clip curves were written as YAML, so scrub them once.
