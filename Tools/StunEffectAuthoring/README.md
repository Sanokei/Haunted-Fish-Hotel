# Authored piranha stun effect

The effect uses the existing `Assets/Art/Fish/Piranha/piranhahigh.blend` model and its original `piranhahigh.png` pixel texture. It contains five miniature 3D piranhas and ten tapered curved strokes on three differently tilted orbit planes, with a continuously looping 1.6-second orbit and gentle vertical bobbing.

## Assets

- Reusable prefab: `Assets/Prefabs/Effects/PiranhaStun.prefab`
- Native Unity meshes: `Assets/Art/Effects/PiranhaStun/PiranhaMini.asset` and `PiranhaCurvedTrails.asset`
- Editable Blender source: `Assets/Art/Effects/PiranhaStun/Source~/PiranhaStun.blend`
- Portable mesh export: `Assets/Art/Effects/PiranhaStun/PiranhaStunMeshes.fbx`
- Legacy animation: `Assets/Art/Effects/PiranhaStun/PiranhaStunOrbit.anim`
- Labelled preview: `Tools/StunEffectAuthoring/PiranhaStun_BlenderPreview.png`

The preview is a Blender asset render, not a Unity or gameplay capture. The `Source~` directory is intentionally excluded from Unity's import pipeline: the native meshes do not require Blender on the player's development machine. Blender source embeds the original pixel texture and includes editable geometry and animation; no images were repainted.

## Binding and lifecycle

A single nested prefab is authored under `HotelNetworkPlayer/Fish`, with the existing `HotelFishSprite` fields `_StunEffect` and `_StunAnimation` referencing its root and Animation component. The nested scale compensates for the parent Fish visual's existing 0.1 scale. Its existing head anchor remains 1.25 world units above the player origin, with the same 20-degree local tilt. The revised internal orbit group is lifted by 0.22 local units so all three tilted planes clear the head. Sampling every frame of the 1.6-second loop puts the lowest mesh point at 1.085 world units, above the approximately 0.979-unit fish crown. Existing player transforms, controller/collider dimensions, and networking settings are unchanged.

The reusable root starts disabled; Animation Play Automatically is off. `HotelFishSprite.SetStunned` owns activating/playing and stopping/deactivating it. Runtime stun end, death and disable/scene cleanup are handled by that existing presentation owner. There is no new runtime component, per-frame controller, Instantiate call, particle construction or material allocation for this effect. The inactive Lobby cinematic fish and diagnostic scene copies are deliberately untouched.

The source model's nose points toward Blender -Y, mapped to Unity +Z. Each fish is 0.25 m long; the two outer orbit planes use a 0.47 m radius and the third uses 0.445 m. Each orbit fish yaw is its radial angle plus 180 degrees, exactly matching the positive-yaw travel tangent. Each of the three unwound 0-to-360-degree Euler curves has a constant 225 degrees/second slope; loop endpoints are the same orientation and all position curves have equal values and derivatives at the seam.

## Budgets

- Miniature piranha: 120 split vertices, 60 triangles, shared by five renderers
- Curves across three meshes: 800 split vertices, 1,560 triangles
- Whole effect: 1,860 triangles, eight renderers, two shared materials, one Animation component
- Native meshes retain no readable CPU copy
- Portable FBX: approximately 54 KiB

## Rebuild

From the repository root:

```sh
blender -b --python Tools/StunEffectAuthoring/author_piranha_stun.py
python Tools/StunEffectAuthoring/author_unity_effect.py
python Tools/StunEffectAuthoring/verify_piranha_stun.py
```

GUIDs are stable and existing metadata is preserved. The binding author inserts the nested effect only if the production player does not already contain stun bindings. The source model's slight UV overscan is preserved rather than altering the artwork.

## Verification status

Source-only checks cover YAML syntax, local references, unique external GUIDs, metadata, native vertex/index buffers, finite values, animation paths, disabled initial state, no duplicate binding, tangent-facing direction throughout all three tilted orbits, periodic position/velocity, sampled head clearance, and triangle budgets. Blender reopens the editable file with the sixteen intended objects and packed texture successfully.

Unity import, shader compilation, Animation playback, rendered scale/occlusion in gameplay, and multiplayer effect delivery still require native Unity verification. They are not established by the asset preview or source checks.

The author-time layout is `Tools/StunEffectAuthoring/orbit_layout.json`. It is not a runtime configuration: the prefab, curves and geometry are fully serialized. The five fish retain angular separation while the longer, thinner curved strokes cross between the tilted planes. The Unity clip contains three rotation curves and five position curves, sampled at 60 fps.
