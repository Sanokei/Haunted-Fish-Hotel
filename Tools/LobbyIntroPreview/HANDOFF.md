# Handoff: lobby intro rework (Haunted Fish Hotel)

Repo `sanokei/haunted-fish-hotel`, branch `main` (no PR unless asked).

## How to run this (orchestrator)
Use **one coding agent** in this repo for the Unity/Blender work, and **one research agent with open internet** for the photo sourcing below
(the previous sandbox could only reach GitHub, which is why no photos were fetched). Have a reviewer agent check the final diff.
Run the research first; the coding agent needs its files.

## House rules (from the owner, "Sano")
- Commit as Sano (`Sano <36809574+Sanokei@users.noreply.github.com>`).
- Author Unity scenes/prefabs (hand-written YAML) rather than building objects in code. Decouple. No Update loops with flag variables; use events/coroutines/animation.
- Use **Blender visual QA** (render it, look at it) before authoring anything that has to look right, and show the owner the finished look.
- Never upload the preview video anywhere. Replies to the owner: short, plain, a little whimsical.
- Nothing here has been opened in Unity (no Unity in the sandbox). Say so; don't claim it is verified.

## What the intro must be (owner's reference video; the owner has the file)
In order:
1. **Opening:** grey-blue fog over a **front-on** city of primitive blocks, each with a **real building-facade photo** on its front, **water tanks and fire escapes** on the roofs (no chimneys), **realistic photographic clouds** above, light rain. Camera level, no angle.
2. **Door scene** (flat UI rhombus panels): knocking on the door, letter slides under it. Wall is **plaster**; doormat is a **green carpet with two gold inner borders**, drawn **in perspective** on the floor.
3. **Hotel** exterior/gate crane (existing parallax rig). No city in this rig.
4. **Lobby interior** (the real warm-orange scene): fish walks in, passes toward reception. A **red runner** carpet leads to the desk; the **floor carpet (repeating shell/damask pattern) is teal**, not orange.
5. **Dolly + tilt up** to reveal a **tunnel of frames**, ending in **darkness**.

Story order is already right in `Assets/Dialogue/LobbyIntroduction.ink` (+ compiled `.json`).

## Done and pushed
- Theatre guide copy (Chaplin/Keaton), boss fight (hot-potato bomb cue, more placeables, arm animations), capsule art: separate, finished.
- Tunnel shot: `Assets/Scripts/Multiplayer/Lobby/LobbyIntroductionWorldSequence.cs` (dolly/tilt curves, FOV), `Assets/Scenes/Lobby.unity` tunnel marker, ink `LobbyShot("tunnel", 6.0)`; tests in `Tools/IntroductionTests`.
- Door scene: `Assets/Resources/LobbyIntroductionUI.prefab` Door section now has `Plaster wall` and `Floor with green mat (Blender perspective)` images; art in `Assets/Art/LobbyIntro/Door/`; generators `Tools/LobbyIntroPreview/make_door_art.py`, `door_mat_perspective.py` (Blender renders the 3D set and the floor strip is cropped from it).
- Lobby floor carpet material tinted teal: `Assets/Art/Materials/LobbyRevision/Lobby_Burgundy_Carpet.mat` (`_BaseColor`/`_Color`). Red runner already exists in the scene (`LobbyCapsuleCarpetRunner.prefab`).
- Opening-city Blender script written and previewed at half res: `Tools/LobbyIntroPreview/city/blender_opening_city.py` (front-on rows Far/Mid/Back/Near, fog by distance, water tanks, fire escapes, volumetric clouds, layers rendered as separate transparent PNGs: FogSky, OpeningCityFar/Mid/Back/Near, CloudBank).
- Prefab authoring script written, **not yet run**: `Tools/LobbyIntroArt/author_opening_city.py`.

## To do
1. **Research agent: source real images, record provenance/licence** (CC0 or similar, like `Assets/Art/LobbyIntro/Wallpaper/LICENSE.txt`).
   - 6-10 building-facade photos, straight-on, windows visible (apartment blocks, brick tenements, warehouses). Save as `Tools/LobbyIntroPreview/city/facades/photo_XX.jpg`. Candidates: Wikimedia Commons, Poly Haven, ambientCG, Unsplash/Pexels (check licence).
   - 2-4 cloud photos (overcast/cumulus, soft edges) to cut out into a transparent cloud layer (key out the sky; match fog palette). Candidates: Wikimedia Commons, Pexels, Unsplash.
2. **Coding agent:**
   - Install bpy (`pip install bpy` in a venv; the old `/tmp/bpyenv` is gone). Cycles CPU, no GPU/EGL.
   - Build the cloud layer from the photos (new small script beside `make_cloud_bank.py`, or keep the Blender volume as a fallback). Output `CloudBank.png` 1600x900 RGBA.
   - Render final 1600x900 layers: `python Tools/LobbyIntroPreview/city/blender_opening_city.py --out Assets/Art/LobbyIntro/City --samples 40` (clouds `--only CloudBank --samples 140`). Look at a composite over `FogSky` and compare to the reference: grey-blue fog, rooftops in the lower two thirds, sky on top, clouds overlapping the skyline. Tune fog colour/rows until it matches.
   - Add `.png.meta` for each (copy an existing sprite meta, new guid) and write `Tools/LobbyIntroArt/opening_city_guids.json` (`{"FogSky": guid, "OpeningCityFar": ..., "CloudBank": ...}`) because `author_opening_city.py` reads it.
   - Run `python Tools/LobbyIntroArt/author_opening_city.py`. It removes the old night-city slabs (ids 5812000000000000101-403) from the gate rig, adds the five layers at the back of the `Storm` group, points both cloud variants at CloudBank, and hides the hotel and tree silhouettes in the opening.
   - Fix `Tools/LobbyIntroArt/test_lobby_intro_art.py`: it still expects 11 parallax layers and the old city order; it should expect the original 8 and assert the new Storm layers. Then delete the old `Assets/Art/LobbyIntro/City/CityFar|Mid|Near.png` and metas (ask the owner first; they dislike surprise deletions).
   - Verify the tunnel shot ends in darkness (look at the curves in `Lobby.unity` and the tunnel prefab 8141014101).
   - Check the lobby: red runner visible, teal carpet. Make a lobby preview that looks like the real warm-orange room (orange walls, red runner, frames on the upper walls); the earlier Blender mock did not and the owner rejected it.
   - Run `python Tools/LobbyIntroArt/test_lobby_intro_art.py` and the `Tools/IntroductionTests` project (net9 projects can run on .NET 8 via a temporary csproj copy). Commit and push with no credit.
3. Show the owner the finished intro (contact sheet / video, kept local) and say plainly that Unity hasn't verified it.

## Gotchas
- Nested-prefab override target id = source fileID XOR the PrefabInstance fileID.
- Tags are not on GameObjects: StoryUI elements are matched by component data; the `Storm` group is `6232796351706565258` (RectTransform), cloud variants are RTs `6996907180247280785` and `8849920227451258767`.
- In Blender, `pkill -f` kills your own shell if the pattern is in the command line. Don't.
- Volume clouds need closed meshes with outward normals and low density (about 0.04); Cycles volumes are noisy below ~100 samples.
