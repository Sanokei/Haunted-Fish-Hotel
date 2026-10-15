#!/usr/bin/env python3
"""Authors the six new placeable defenses (bumper, ramp, dynamite crate for each side) and their trays.

    python Tools/BossFightTests/HotPotatoAuthoring/author_pieces.py

Adds, inside BossDEFENSES.prefab (so the scene instance picks them up):
  * 6 piece GameObjects with Rigidbody, a collision collider, a pickup trigger, BossDefensePiece and primitive-built art
    for the healthy / cracked / broken / preview states;
  * a second tray column per side, cloned from the scene's existing trays and shifted outward.
Then, in BossFight.unity, extends the defense board's `_Pieces` to twelve (stripped references) and widens the camera's
frame so the outer trays fit. Slot layout: 6-8 fish bumper/ramp/crate, 9-11 ghost bumper/ramp/crate.
Idempotent (refuses to run twice). Not validated in the Unity editor.
"""
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import animkit as ak  # noqa: E402
import author_bomb as ab  # noqa: E402
import unity_yaml as uy  # noqa: E402

ROOT = ab.ROOT
DEFENSES = ROOT / 'Assets/Resources/BossFight/SceneObjects/BossDEFENSES.prefab'
VIEWS = ROOT / 'Assets/Resources/BossFight/SceneObjects/BossVIEWS.prefab'
SCENE = ab.SCENE
DEF_GUID = 'e07d42519125a2248ab23a5867942da6'
RACK_TR = '3766437901658497848'
INSTANCE = '2144419097'
BOARD = '470086783'
BOUNCE_GUID = 'eb6b43acbfc6143488e308087a7f45c3'
EXTERNAL_MATS = {'Valid': 'cf8427a262849c343b2f8d16f1511638', 'Invalid': 'ff49621d761e7b748b0267eb1e539330', 'Crack': '624a0b18f88785449957e60f41d19cfc'}
NEW_MATS = {
    'HP_BumperBase': (0.12, 0.14, 0.16), 'HP_BumperBody': (0.85, 0.18, 0.28), 'HP_BumperCap': (0.96, 0.90, 0.80),
    'HP_RampMetal': (0.45, 0.60, 0.75), 'HP_CrateWood': (0.62, 0.45, 0.25), 'HP_Dynamite': (0.80, 0.10, 0.08),
}
FISH_X, GHOST_X = 994.90, 1005.10
TRAY2 = {'Fish': (994.60, -2.0), 'Ghost': (1005.40, 2.0)}
FRAME_WIDTH = 12.2


def physics_material():
    template = (ROOT / 'Assets/Resources/BossFight/Defenses/DefenseBounce.physicMaterial').read_text(encoding='utf-8')
    text = template.replace('m_Name: DefenseBounce', 'm_Name: BumperBoost').replace('m_Bounciness: 1\n', 'm_Bounciness: 1.3\n')
    path = ROOT / 'Assets/Resources/BossFight/Defenses/BumperBoost.physicMaterial'
    ab.write(path, text)
    guid = ak.guid('physics/BumperBoost')
    ab.write(path.with_name(path.name + '.meta'), f'fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 13400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return guid


def collider_doc(kind, fid, go, material, trigger, enabled, size=None, radius=None, height=None, centre=(0, 0, 0)):
    head = (f'--- !u!{kind[0]} &{fid}\n{kind[1]}:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n'
            f'  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Material: {material}\n  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n'
            f'  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_LayerOverridePriority: 0\n  m_IsTrigger: {trigger}\n  m_ProvidesContacts: 0\n  m_Enabled: {enabled}\n')
    c = ab.vec3(centre)
    if kind[1] == 'BoxCollider':
        return head + f'  serializedVersion: 3\n  m_Size: {ab.vec3(size)}\n  m_Center: {c}\n'
    if kind[1] == 'SphereCollider':
        return head + f'  serializedVersion: 3\n  m_Radius: {radius}\n  m_Center: {c}\n'
    return head + f'  serializedVersion: 2\n  m_Radius: {radius}\n  m_Height: {height}\n  m_Direction: 1\n  m_Center: {c}\n'


BOX, SPHERE, CAPSULE = (65, 'BoxCollider'), (135, 'SphereCollider'), (136, 'CapsuleCollider')


def rigidbody_doc(fid, go):
    return (f'--- !u!54 &{fid}\nRigidbody:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n'
            f'  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 5\n  m_Mass: 1\n  m_LinearDamping: 0\n  m_AngularDamping: 0.05\n'
            '  m_CenterOfMass: {x: 0, y: 0, z: 0}\n  m_InertiaTensor: {x: 1, y: 1, z: 1}\n  m_InertiaRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_IncludeLayers:\n'
            '    serializedVersion: 2\n    m_Bits: 0\n  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_ImplicitCom: 1\n  m_ImplicitTensor: 1\n'
            '  m_UseGravity: 0\n  m_IsKinematic: 1\n  m_Interpolate: 0\n  m_Constraints: 116\n  m_CollisionDetection: 0\n')


# kind -> (radius, healthy art, collision spec, pickup spec); art = (name, prim, material, pos, scale, rot)
def art_for(kind, side):
    tilt = 35 * (1 if side == 'Fish' else -1)
    if kind == 'Bumper':
        return ([('Base', 'cylinder', 'HP_BumperBase', (0, .08, 0), (.84, .08, .84), None), ('Body', 'cylinder', 'HP_BumperBody', (0, .3, 0), (.62, .16, .62), None),
                 ('Cap', 'sphere', 'HP_BumperCap', (0, .52, 0), (.5, .36, .5), None)], .42)
    if kind == 'Ramp':
        return ([('Plate', 'cylinder', 'HP_BumperBase', (0, .03, 0), (.9, .03, .9), None), ('Angled wall', 'cube', 'HP_RampMetal', (0, .22, 0), (.95, .38, .14), (0, tilt, 0))], .48)
    return ([('Crate', 'cube', 'HP_CrateWood', (0, .28, 0), (.56, .56, .56), None), ('Stick 1', 'cylinder', 'HP_Dynamite', (-.14, .72, 0), (.1, .16, .1), None),
             ('Stick 2', 'cylinder', 'HP_Dynamite', (0, .72, 0), (.1, .16, .1), None), ('Stick 3', 'cylinder', 'HP_Dynamite', (.14, .72, 0), (.1, .16, .1), None),
             ('Fuse', 'cylinder', 'HP_Wick', (0, .96, 0), (.03, .1, .03), None)], .34)


def collision_for(kind, side, go, mats):
    tilt = 35 * (1 if side == 'Fish' else -1)
    bounce = f'{{fileID: 13400000, guid: {BOUNCE_GUID}, type: 2}}'
    if kind == 'Bumper':
        return ('Bumper', SPHERE, dict(radius=.42, centre=(0, .3, 0)), f'{{fileID: 13400000, guid: {mats["bumper_physics"]}, type: 2}}')
    if kind == 'Ramp':
        return ('Ramp', BOX, dict(size=(.95, .38, .14), centre=(0, .22, 0)), bounce)
    return ('Crate', BOX, dict(size=(.56, .56, .56), centre=(0, .28, 0)), bounce)


def clone_tray(scene, root_tr, new_parent, name, position, taken):
    """Clone a tray subtree from the scene into prefab text with fresh ids."""
    order, stack = [], [root_tr]
    while stack:
        t = stack.pop(0)
        order.append(t)
        stack += scene.children(t)
    docs = []
    mapping = {}
    for t in order:
        go = scene.docs[t].ref('m_GameObject')
        for old in [t, go] + scene.comps(go):
            mapping.setdefault(old, ab.new_id())
    text = ''
    for old in list(mapping):
        d = scene.docs[old]
        body = d.body
        for k, v in mapping.items():
            body = re.sub(r'\{fileID: %s\}' % k, '{fileID: %s}' % v, body)
        text += f'--- !u!{d.ty} &{mapping[old]}\n{body if body.endswith(chr(10)) else body + chr(10)}'
    # re-root
    rt = mapping[root_tr]
    text = re.sub(r'(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Father: )\{fileID: \d+\}' % rt, r'\g<1>{fileID: %s}' % new_parent, text)
    text = re.sub(r'(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_LocalPosition: )\{[^}]*\}' % rt, r'\g<1>' + ab.vec3((position[0], 1.62, position[1])), text)
    text = text.replace('m_Name: ' + scene.go_of(root_tr).name, 'm_Name: ' + name, 1)
    return rt, text


def main():
    text = DEFENSES.read_text(encoding='utf-8')
    if 'Fish bumper defense' in text:
        sys.exit('BossDEFENSES.prefab already has the new pieces.')
    ab.author_scripts()
    mats = ab.author_materials(NEW_MATS)
    mats = {**mats, **{k: v for k, v in ab.author_materials().items()}, **{f'ext_{k}': v for k, v in EXTERNAL_MATS.items()}}
    mats['Valid'], mats['Invalid'], mats['Crack'] = EXTERNAL_MATS['Valid'], EXTERNAL_MATS['Invalid'], EXTERNAL_MATS['Crack']
    mats['bumper_physics'] = physics_material()
    f = uy.File(str(DEFENSES))
    # renderer template from the cue ball's renderer in BossTABLE (any plain MeshRenderer works)
    table = uy.File(str(ab.TABLE))
    mr = table.docs['5115464176767574246'].body.rstrip('\n') + '\n'
    mr = re.sub(r'  m_GameObject: \{fileID: \d+\}', '  m_GameObject: {fileID: {GO}}', mr)
    mr = re.sub(r'  m_Materials:\n  - \{[^}]*\}', '  m_Materials:\n  - {fileID: 2100000, guid: {MAT}, type: 2}', mr)
    mr = mr.replace('m_CastShadows: 1', 'm_CastShadows: 0')
    b = ab.Builder(f, mr, mats)
    piece_comps = []
    slot = 6
    for side, x0, zsign in (('Fish', FISH_X, -1), ('Ghost', GHOST_X, 1)):
        for k, kind in enumerate(('Bumper', 'Ramp', 'Crate')):
            art, radius = art_for(kind, side)
            go, tr = ab.new_id(), ab.new_id()
            rb, col, pick, mono = ab.new_id(), ab.new_id(), ab.new_id(), ab.new_id()
            pos = (x0, 1.62, zsign * (k + 1))
            # children first so the group ids exist for the piece's references
            groups = {}
            for gname, active in (('Healthy', 1), ('Crack stage one', 0), ('Crack stage two', 0), ('Broken fragments', 0)):
                groups[gname] = b.node(f'{side} {kind} {gname}', tr, (0, 0, 0), (1, 1, 1), active=active)
            for (name, prim, mat, p, sc, rot) in art:
                b.node(name, groups['Healthy']['tr'], p, sc, prim, mat, rot=rot)
            # damage reads as dark slabs; debris as small tumbled cubes
            b.node('Crack A', groups['Crack stage one']['tr'], (.0, .3, -.24), (.34, .03, .02), 'cube', 'Crack')
            b.node('Crack B', groups['Crack stage two']['tr'], (.1, .4, .24), (.3, .03, .02), 'cube', 'Crack', rot=(0, 0, 25))
            rubble = {'Bumper': 'HP_BumperBody', 'Ramp': 'HP_RampMetal', 'Crate': 'HP_CrateWood'}[kind]
            for r, (rx, rz, s) in enumerate(((-.22, .1, .16), (.18, -.15, .12), (.05, .25, .1), (-.1, -.25, .14))):
                b.node(f'Fragment {r + 1}', groups['Broken fragments']['tr'], (rx, s / 2, rz), (s, s, s), 'cube', rubble, rot=(0, 20 * r, 15 * r))
            valid = b.node('Valid preview', tr, (0, .03, 0), (radius * 2, .02, radius * 2), 'cylinder', 'Valid', active=0)
            invalid = b.node('Invalid preview', tr, (0, .03, 0), (radius * 2, .02, radius * 2), 'cylinder', 'Invalid', active=0)
            # collision collider (may sit on a tilted child so the ramp deflects at its true angle)
            cname, ckind, cargs, cmat = collision_for(kind, side, go, mats)
            if kind == 'Ramp':
                holder = b.node('Ramp collider', tr, (0, 0, 0), (1, 1, 1), rot=(0, 35 * (1 if side == 'Fish' else -1), 0))
                cargs = dict(size=(.95, .38, .14), centre=(0, .22, 0))
                col_go = holder['go']
                collision = collider_doc(ckind, col, col_go, cmat, 0, 0, **cargs)
                # the holder GameObject must list the collider
                b.docs = [d.replace(f'  - component: {{fileID: {holder["tr"]}}}\n', f'  - component: {{fileID: {holder["tr"]}}}\n  - component: {{fileID: {col}}}\n', 1)
                          if d.startswith(f'--- !u!1 &{holder["go"]}\n') else d for d in b.docs]
            else:
                collision = collider_doc(ckind, col, go, cmat, 0, 0, **cargs)
            pickup = collider_doc(CAPSULE, pick, go, '{fileID: 0}', 1, 1, radius=round(radius + .1, 3), height=1.15, centre=(0, .42, 0))
            comps = [tr, rb] + ([pick] if kind == 'Ramp' else [col, pick]) + [mono]
            b.docs.append(ab.go_doc(go, f'{side} {kind} defense', comps))
            b.specs.append((tr, go, RACK_TR, pos, (1, 1, 1), None))
            b.children.setdefault(RACK_TR, []).append(tr)
            b.docs += [rigidbody_doc(rb, go), collision if kind != 'Ramp' else collision, pickup]
            b.docs.append(ab.mono_doc(mono, go, 'fdaf7aa7647716644991cba4f1ab8dad', 'BossDefensePiece', [
                ('_Slot', str(slot)), ('_Body', f'{{fileID: {rb}}}'), ('_Collision', f'{{fileID: {col}}}'), ('_Pickup', f'{{fileID: {pick}}}'),
                ('_Healthy', f'{{fileID: {groups["Healthy"]["go"]}}}'), ('_Crack1', f'{{fileID: {groups["Crack stage one"]["go"]}}}'),
                ('_Crack2', f'{{fileID: {groups["Crack stage two"]["go"]}}}'), ('_Broken', f'{{fileID: {groups["Broken fragments"]["go"]}}}'),
                ('_ValidPreview', f'{{fileID: {valid["go"]}}}'), ('_InvalidPreview', f'{{fileID: {invalid["go"]}}}')]))
            piece_comps.append(mono)
            slot += 1
    # trays cloned from the scene
    scene = uy.File(str(SCENE))
    tray_docs = ''
    new_roots = []
    for label, src in (('Fish', '969976688'), ('Ghost', '1974223225')):
        rt, doc = clone_tray(scene, src, RACK_TR, f'{label} side tray 2 - three authored cells', TRAY2[label], None)
        tray_docs += doc
        new_roots.append(rt)
    # splice into the prefab: rack children, then every new document
    all_new = [tr for tr, *_ in b.specs if False]
    rack_children = [c for c in b.children.get(RACK_TR, [])] + new_roots
    pat = re.compile(r'(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:\n(?:  - \{fileID: -?\d+\}\n)+)' % RACK_TR)
    m = pat.search(text)
    assert m, 'rack transform'
    text = text.replace(m.group(1), m.group(1) + ''.join(f'  - {{fileID: {c}}}\n' for c in rack_children), 1)
    b.docs += [ab.transform_doc(tr, go, parent, b.children.get(tr, []), pos, scale, rot) for tr, go, parent, pos, scale, rot in b.specs]
    text = text.rstrip('\n') + '\n' + ''.join(b.docs) + tray_docs
    DEFENSES.write_text(text, encoding='utf-8')
    wire_scene(piece_comps)
    print('authored 6 pieces and 2 trays; piece components', piece_comps)


def wire_scene(piece_comps):
    text = SCENE.read_text(encoding='utf-8')
    stripped, refs = '', ''
    for comp in piece_comps:
        sid = ab.new_id()
        refs += f'  - {{fileID: {sid}}}\n'
        stripped += (f'--- !u!114 &{sid} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: {comp}, guid: {DEF_GUID}, type: 3}}\n'
                     f'  m_PrefabInstance: {{fileID: {INSTANCE}}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
                     f'  m_Script: {{fileID: 11500000, guid: {ak.guid("script/BossDefensePiece") if False else "fdaf7aa7647716644991cba4f1ab8dad"}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n')
    m = re.search(r'(--- !u!114 &%s\nMonoBehaviour:\n(?:.*\n)*?  _Pieces:\n(?:  - \{fileID: -?\d+\}\n)+)' % BOARD, text)
    assert m, 'board _Pieces'
    text = text.replace(m.group(1), m.group(1) + refs, 1)
    marker = '--- !u!1660057539 &9223372036854775807\n'
    text = text.replace(marker, stripped + marker, 1)
    text = text.replace('_FrameSize: {x: 7.3, y: 1.5, z: 12.65}', '_FrameSize: {x: %s, y: 1.5, z: 12.65}' % FRAME_WIDTH)
    SCENE.write_text(text, encoding='utf-8')
    v = VIEWS.read_text(encoding='utf-8')
    VIEWS.write_text(v.replace('_FrameSize: {x: 7.3, y: 1.5, z: 12.65}', '_FrameSize: {x: %s, y: 1.5, z: 12.65}' % FRAME_WIDTH), encoding='utf-8')


if __name__ == '__main__':
    main()
