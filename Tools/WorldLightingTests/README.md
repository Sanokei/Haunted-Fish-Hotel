# Authored 2D artwork in 3D lighting

The physical world-art cards now cast original-alpha silhouettes and receive URP 3D main/punctual shadows. Screen-space Images, cursor/placement graphics, glow/weather VFX and music-wave feedback remain nonphysical graphics. No runtime GameObjects, UI, meshes, masks, lights or materials were added.

## Ownership and source

- `Assets/Art/WorldLighting/ArtLighting.hlsl`: one thin-sheet main/additional lighting policy; Forward and Forward+ light loops, cascaded per-fragment atlas lookup and two-sided Lambert response.
- `ArtSpritePass.hlsl`: common visible/shadow vertices, preserving sprite atlas UVs, flip, tint, external alpha and skinning. The shadow pass clips original alpha at 0.2. It includes the Boss arm's existing local root alpha fade.
- Existing `HallwaySpriteLit`, `LobbySpriteFog` and `BossArmVoidFade` retain their shader identities. `WorldSpriteLit.mat` is explicitly assigned to physical fish, trap and Boss cards. Existing renderer flags are authored TwoSided/receive-shadows.
- `WatchingEye.shader` keeps one visible composite per frame, with a separate depth-only shadow pass. The original frame/backing alpha supplies its silhouette; no pupil texture is sampled while casting. Frame/eye outline/grey backing and future picture surfaces receive light, shadows and fog. Pupil RGB is added after illumination and remains independent of light/shadow response. The whole composed frame, including pupils, still receives atmospheric fog/height/distance attenuation. Exact masks, gaze, closure and cinematic `_PresentationGain` remain intact. There is no stencil usage.
- `_HasEye=0` disables pupil and outline for intentionally empty grey frames. `_HasPicture=0` keeps the future picture slot empty. A future picture can be bound to `_PictureTex`, enabled with `_HasPicture`, and fitted using `_PictureRect` (center XY, size ZW in frame UV).
- `author_world_lighting.py` is an idempotent offline renderer/material migration. `author_room_atmosphere.py` owns independent authored atmosphere/light fields and the staircase lining prefab. Gallery and sconce generators preserve the new lighting defaults.

## Bounded scene lighting and darkness

Exact values and source IDs are in `room-atmosphere.json`:

- Lobby existing main directional `1998747293`: soft shadows, intensity 0.35, Euler (50,-30,0).
- Lobby reception point `1555505994` and fireplace-bounce point `1060684691`: soft shadows enabled, existing energy/range retained. Exactly two punctual shadow sources; other point lights remain shadowless. Both use authored URP AdditionalLightData with pipeline bias disabled and medium512px PC resolution tier. The Lobby directional and hallway key also explicitly use their serialized bias rather than silently inheriting the pipeline defaults.
- Boss existing cream-table point key `672667459` keeps its hard shadow, energy, range and bias; authored URP data `981041` now makes that bias effective, with 1024px PC tier. Theatre existing screen-spill spotlight `1297839878` gains soft shadows and URP data `981031` with 512px PC tier. No lights were added to either scene; exact fields are in `existing-key-shadows.json` and reproduced by `author_existing_scene_shadow_keys.py`.
- Stair threshold point `184464686`: intensity 0.8, range 3.4 to limit spill beyond the arch.
- GameHallway contains one authored directional shadow key `981003`, intensity 0.75, color (0.65,0.74,1), Euler (50,-30,0). Game and tutorial recording inherit it. All sixty sconce point lights remain shadowless.
- Upper wall material copies original `HotelLobbyColor6` color. Both the original three upper walls and the intro-only continuous extension use this finish, so there is no bright material seam. Wall/art color fades from multiplier 1 at world Y=4.25 to 0.012 at Y=12. Nearby frame composites use max(height visibility, 0.1 × (1 − smoothstep(2.7,9,camera distance))) so the original painted borders retain faint readable shape. This atmospheric attenuation includes pupils; their exemption is from light/shadow response. Playable-room artwork below the cornice retains normal illumination. Intro-only frame materials use near visibility 0.8 over 9.6–32 units, matching the 15–25-unit visible upper-room distances from the endpoint camera. Intro material ambient is 0.8; its pupil display intensity is 0.125, so the stronger near-art visibility lifts original painted rims without raising pupil energy. Normal Lobby ambient 0.32 and Hallway 0.52, near visibility 0.1 over 2.7–9 units, and full original pupil intensity remain unchanged. Existing cinematic distance gain remains last.
- Three serialized depth-writing black lining planes enclose the far staircase back/outer side/top, with no colliders or runtime components. They do not cast shadows into the room. Lobby camera clears to black; fog is dark neutral-blue (0.018,0.021,0.03), 12–38 units.
- PC already receives transparent shadows. Mobile's transparent-shadow receiver gate is enabled; its existing main-shadow support remains. Additional/punctual mobile shadow maps stay disabled. This preserves the bounded mobile lighting budget, but point-light shadow reception remains PC-only.

## Checks

```
python Tools/WorldLightingTests/verify_world_lighting.py
python Tools/WatchingFrameTests/verify_authored_galleries.py
python Tools/WatchingFrameTests/verify_intro_frame_tunnel.py
python Tools/HallwayLighting/verify_sconces.py
bash Tools/WorldLightingTests/check_unity_api.sh /path/to/Unity/Editor/Data
```

The source suite checks alpha holes, flipped-card parity, hidden alpha, frame/backing union, no pupil caster sampling, unlit-pupil composition, both light/shadow paths, real URP symbols, authored renderer flags, a bounded shadow-light count and untouched playable-height response. The Editor validator compiles against actual Unity 6000.3.6f1 assemblies, without Unity doubles. In an available Editor, run **Tools > Haunted Fish > Validate world artwork shadows**, then inspect moving fish/trap/frame shadows and profile target devices.

Native Unity cannot launch here because IPC initialization is denied. Source checks and C# API compilation do not prove GPU shader import, runtime light selection, camera appearance, collision, or performance. Blender proofs are source-derived approximations and explicitly labeled; Cycles lighting is not URP. No native pass is claimed.

Official references:
- https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/use-built-in-shader-methods/shadows
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Renderer-shadowCastingMode.html
- Installed Unity 6000.3.6f1 / URP17.3 `ShadowCasterPass.hlsl`, `Shadows.hlsl` and `RealtimeLights.hlsl` provide the version-matched APIs used here.

## Final source checkpoint

`joint-lighting-gallery-freeze.json` records exact shader/material/generator hashes and the three lighting-owned renderer fields for selective publication. Rebuild it with `python Tools/WorldLightingTests/freeze_world_lighting.py` only after regression/replay checks. The approved intro final-camera source proof measured nearest left painted contours at 15.6% median display luminance and right at 8.7% (90th percentile 12.0%), with the center dark and pupils restrained. Ordinary gallery profiles remain unchanged. These measurements describe the Blender approximation, not native Unity. The renderer owns the continuous ending motion proof.
