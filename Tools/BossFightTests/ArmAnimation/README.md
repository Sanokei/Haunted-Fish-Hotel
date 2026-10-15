# Boss arm animation

The boss is a head plus eight tentacle sprites. Each tentacle is a full-canvas sprite pivoting at its canvas centre, so
a plain rotation would swing it around the wrong point. Every clip rotates a tentacle about its **shoulder** (scaled, and added to its rest rotation) (where it
meets the body) by pairing the rotation with a position offset; `rig_data.py` finds shoulders from the art and reads
the scene's *effective* rest layout: prefab values plus the ~250 overrides `BossFight.unity` applies (tentacles sit low, stretched 1.25-1.65x). Clips must start from that layout or the boss jumps out of place.

| State | Plays when | What the arms do |
| --- | --- | --- |
| Idle | default | lazy ripple down each side, an occasional curl toward the middle |
| Juggle | cue bomb lit | sides alternate, lifting and snapping as if passing a hot potato |
| Panic | fuse past 65% heat | every arm trembles and flails, head shakes |
| Frustrated | fish scored | tentacles rear back one after another, then slam the table |
| Gloat | ghost scored, or bomb blew up on the fish side | a wave runs along the tentacles, head bobs |
| Flinch | bomb blew up on the boss's side | arms fold over the head, hard duck, slow recovery |
| Defeat | fish wins the match | tentacles go limp, arc outward and sink |
| Triumph | ghost wins the match | arms thrown up and wide, rippling fast |

`poses.py` holds the motion as small functions; `author_arm_clips.py` samples them into `.anim` clips and one
`AnimatorController` per boss variant (variant 3's rest positions differ slightly). `preview.py` composites the real
sprites with the same functions (`pose-sheet.png`). `BossSegmentedRig` has no per-frame code; it only selects states,
stretching one-shot clips to the arena's reaction window through the controller's `Speed` parameter.

Regenerate: `python author_arm_clips.py`. **Not validated in the Unity editor**: import `Assets/Animations/BossFight/Arms`,
scrub each clip, and confirm the Animator on each variant root plays Idle.
