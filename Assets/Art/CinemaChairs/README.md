# Haunted Fish Hotel — Cinema Chairs

Original 3D geometry authored and rendered in Blender 4.3.2. These are reusable theatre-chair meshes, not flat silhouette cards. The uploaded Unity screenshots informed the dark foreground use; they do not establish the project's real-world scale.

## Files

- `Models/HFH_CinemaChair_LOD0.fbx`: individual chair, 3,964 triangles.
- `Models/HFH_CinemaRow3.fbx`: three seats, 11,892 triangles.
- `Models/HFH_CinemaRow5.fbx`: five seats, 19,820 triangles.
- `Models/HFH_CinemaSplit6.fbx`: two three-seat banks with a 1.41 m clear middle gap, 23,784 triangles.
- `Textures/HFH_Velvet_Weave_BaseColor.png`: optional neutral 256 px seamless fabric tile. The supplied FBX uses solid-color materials and does not depend on the tile.
- `materials.json`: exact linear colors, sRGB swatches, metallic/roughness and Unity smoothness values.
- `mesh_qa.json`, `uv_qa.json`, `fbx_roundtrip_qa.json`: measured geometry and FBX round-trip checks.
- `Source/`: editable generation and verification scripts. Update ROOT for another computer.

The separate `HFH_CinemaChairs_Master.blend` contains the editable mesh assets, materials, and two lit presentation setups. The Blender master is intentionally separate from the lightweight Unity ZIP.

## Dimensions and orientation

One Blender unit / one Unity unit = one meter. Apply import scale 1. The individual chair measures 0.690 m wide × 0.734 m deep × 1.074 m high. Seat spacing is 0.720 m. All FBX origins are floor-centered; transforms are baked. Exported up is +Y and seat-facing direction is +Z for Unity. In the editable Blender file up is +Z and seat-facing direction is -Y.

Row widths: 3-seat 2.130 m; 5-seat 3.570 m; split 6-seat 5.670 m. All rows share the individual chair depth/height. The split row's clear center opening is 1.410 m.

## Unity import

1. Copy the Models folder into the target project. Import normals, calculate tangents (MikkTSpace), and leave scale factor at 1.
2. Remap four material slots using the names and values in materials.json. Use the target project's Lit shader (URP/HDRP/Standard as appropriate). Unity smoothness = 1 − roughness. Material workflow differences can require remapping after FBX import.
3. Keep Mesh Collider generation OFF. These are decorative foreground assets; no collision or behavior scripts are included.
4. For many repeated seats, reuse the single-chair mesh with shared materials / instancing. Use row meshes when one static authored chunk is easier to place.
5. For the black silhouette appearance, use the screen/projector as the bright background, keep chair fronts/rears weakly lit, and reserve only a subtle cool rim light. Do not set every material to pure emissive black; the shaped edges should still catch a trace of light.

## Preserve the A/D walking lane

The screenshot shows horizontal movement. Place seating in the foreground/background depth layers, outside the player's capsule path; do not insert new colliders across the horizontal lane. The optional center-gap row is an architectural variant, not an assumption that the player's movement changes to a center aisle. Scene scale, camera framing and real player clearance must be verified in the target Unity project before final placement.

## Modeling and validation

Rounded arched backs, front and rear upholstery panels, thick seat cushions, arm pads, paired feet, structural posts, hinge caps and restrained bronze inlays are all actual geometry. Four material slots; one non-overlapping per-chair UV layer. Repeated chairs in row assets intentionally reuse the same UV layout. All exported geometry is triangulated. All closed components have outward normals / positive signed volume, zero nonmanifold edges and zero degenerate faces. UVs are finite and within 0–1. FBX export/reimport preserves dimensions and triangle count.

Intersecting closed components are intentional assembly construction, not a single merged watertight volume. No rigging or folding animation is supplied. No actual Unity Editor import or scene placement is claimed by this asset bundle; that belongs to the integration pass.

## Preview scope

The studio render shows front/back construction under inspection lighting. The theatre render demonstrates silhouettes against a lit blank screen. It is a Blender lighting mockup, not a screenshot of the user's Unity scene. No external film still or uploaded screenshot is packaged as a texture.

Original procedural geometry and optional fabric tile; no third-party asset dependency.
