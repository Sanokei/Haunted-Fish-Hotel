# Haunted Intertitle: static TextMeshPro asset

The original font's companion TMP asset is `Assets/Art/Fonts/HauntedIntertitle SDF.asset`. It references the original `HauntedIntertitle-Regular.ttf` by GUID, a standalone material, and a real 2048×2048 RGBA SDF atlas. These are committed authored data, not a placeholder waiting for a runtime bake. No fallback typeface, system font or external font-outline source is used.

## Reproducible CPU build

Requirements: Python 3, fontTools, Pillow, NumPy, SciPy and PyYAML for validation. The first four build dependencies were available as fontTools 4.61.1, Pillow 12.3.0, NumPy 2.3.5 and SciPy 1.17.0; byte-for-byte PNG reproduction assumes the same dependency versions.

```sh
python Tools/Fonts/build_intertitle_tmp.py
python -m unittest discover -s Tools/Fonts -p 'test_intertitle_tmp.py' -v
python Tools/Fonts/render_intertitle_tmp_proof.py
```

The builder reads the TTF's complete Unicode cmap, original glyph IDs, outline bounds, advances, face metrics and deliberately authored kerning. Every mapped glyph is packed; atlas exhaustion is an error rather than silent truncation. Spacing characters retain advances with zero-area rectangles. The atlas is rendered at 96 pixels per em with 8× FreeType supersampling and an exact Euclidean distance transform. Signed distance is averaged to the target grid before 8-bit quantization. The threshold is 0.5; alpha changes by `1/(2×13)` per atlas pixel. The material's `_GradientScale` is 13 and TMP padding is 12. One extra guard texel isolates every padded glyph.

Glyph rectangles exclude the padded distance field and use Unity's bottom-left texture coordinates. The PNG uses top-left scanlines, with matching rectangle conversion. The texture importer preserves linear, readable, uncompressed RGBA data with bilinear filtering, clamp wrapping, no mipmaps and no alpha-color dilation. The standard checked-in `TextMeshPro/Distance Field` shader samples this alpha field. Static atlas population prevents a runtime font replacement. All asset GUIDs are stable across rebuilds.

Serialization is based on the shipped font assets and the Unity 6000.3.6f1 bundled `com.unity.ugui` 2.0.0 TMP source. The installed engine's actual managed `FaceInfo` fields and `GlyphRenderMode.SDF8` value (8230) were independently inspected. Material and TMP script references resolve to existing project/package GUIDs. The build manifest includes source and output hashes, character/glyph counts and explicit native-verification status.

Tests independently compare the committed asset against the TTF, check every character/metric/kerning pair, resolve all material/atlas/source references, verify disjoint padded rectangles, and compare reconstructed atlas contours against separately rendered 4× FreeType masks. They also regenerate all artifacts in a temporary directory to check byte identity and stable GUIDs. The CPU proof is reconstructed from the actual serialized TMP tables and atlas; it is not a screenshot from Unity.

## Native validation still required

The current cloud Editor could not start because its socket IPC initialization returned EPERM. No native TTF import, TMP render or gameplay capture is claimed. The checked-in native validator compiles against the installed Unity 6000.3.6f1 real managed engine and TMP assemblies, but a compilation check is not a successful import.

After opening the project in a supported Unity 6000.3.6f1 Editor and letting it import normally, run:

`Tools → Haunted Fish → Typography → Validate original intertitle TTF and TMP`

This explicit command checks the imported source, shader/material/atlas links, native FontEngine glyph indices, real atlas alpha and TMP mesh generation using a temporary unsaved object. It does not rebake or replace the authored assets, save scenes or produce footage. Then visually inspect the font at guide sizes and in the theatre. The menu's PASS log does not replace native visual/capture review.

## Guide integration

`Tools/TheatreVideos/intertitle_cards.py` uses the original TTF directly for final offline cards. This is the same source used for the TMP asset. The original font also replaces the diagnostic legacy-UI recording captions in the authored stage and Editor authoring path. The final guides are videos, so their intertitle pixels come from the offline renderer; the supplied TMP asset is directly assignable to native TMP text without a separate runtime bake. The game menu's existing shared Modak/Varela Round typography is not changed.

The new card proofs live under `Tools/TheatreVideos/CardPreviews/HauntedIntertitle-v1/`. Earlier Smythe previews remain as historical artifacts. Neither the build nor the proof scripts write MP4 files. The verified-real-capture gate remains mandatory before replacing either shipped guide.
