# Source-only scene presentation checks

Run from the repository root, with no extra packages:

```sh
python3 -m unittest discover -s Tools/ScenePresentationTests -p 'test_*.py' -v
python3 Tools/ScenePresentationTests/verify_scene_assets.py
```

The default baseline is `HEAD`. After committing, compare against the patch's parent revision, for example `--base HEAD^`. Use `--asset Assets/path/to/file.prefab` to add an asset to the default Lobby/Boss scene, camera-rig and dolly-atmosphere scope. All modified and untracked Assets files are included automatically. The script is read-only. If PyYAML is already installed, it also parses every inspected YAML object; otherwise that separate syntax stage is explicitly reported as unrun.

Coverage:
- Unity YAML object IDs, local references (including negative IDs and stripped prefab records), component ownership and authored transforms.
- Paired metadata, identity preservation and GUID collisions for changed assets; reference target IDs for available serialized assets. Built-in Unity IDs and the implicit prefab main-asset ID are handled separately from local IDs.
- Existing defects are distinguished from patch-introduced reference regressions. Unknown external/package GUIDs are reported separately because this checkout may not contain Unity's restored package cache.
- The actual BossFight scene's `BossArenaView` references, modular camera prefab and numeric scene overrides. Three perspective views, no-roll own-end orientation, board/goal envelope, clip planes, and authored framing markers are checked at seven landscape/portrait/ultrawide aspects.
- Lobby behavior/audio/camera/physics/UI records and transforms are preserved. Boss scene/table/defense/character physics and table/defense/character transforms are preserved (except the explicitly resized decorative fish artwork); room collider records are preserved while room transforms may change.
- Authored mesh vertex-buffer lengths, index ranges and submesh ranges; carpet winding and floor-lightning height alignment.
- Source guard checks for optional authored stage-light activation/cleanup and viewer-specific framing; these are not runtime lifecycle tests.
- Changed production scripts are checked for newly introduced `GameObject`, `AddComponent` or `CreatePrimitive` construction.

The geometry check uses a double-precision Python transcription of the existing perspective-fit equations. It does not execute or compile production C#, resolve Unity importers, detect visual occlusion, inspect pixels, simulate physics, or prove connected multiplayer. Run current native Unity checks separately. The historical BossInputAndSceneTests single-arena-prefab assumption does not describe the current modular saved scene.
