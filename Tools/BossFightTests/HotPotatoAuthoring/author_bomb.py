#!/usr/bin/env python3
"""Authors the cue-bomb visuals and wires them into the boss arena. Run once from the repository root:

    python Tools/BossFightTests/HotPotatoAuthoring/author_bomb.py

Writes Unity YAML directly (no Editor needed):
  * script .meta files with fixed GUIDs for the new boss scripts;
  * flat materials, three AnimatorControllers and their AnimationClips under Assets/Animations/BossFight/HotPotato;
  * new objects inside BossTABLE.prefab: a glowing fuse on the cue ball, a free-standing blast object, a stun marker
    on each paddle, plus the BossCueBomb / BossCueBombView components;
  * BossFight.unity: the `_Defenses` override, stripped component references and the arena's `_Bomb` / `_BombView`.
Idempotent: it refuses to run twice. Nothing here is validated in the Unity editor yet.
"""
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import animkit as ak  # noqa: E402
import unity_yaml as uy  # noqa: E402

ROOT = Path(__file__).resolve().parents[3]
TABLE = ROOT / 'Assets/Resources/BossFight/SceneObjects/BossTABLE.prefab'
SCENE = ROOT / 'Assets/Scenes/BossFight.unity'
ANIM = ROOT / 'Assets/Animations/BossFight/HotPotato'
MATS = ROOT / 'Assets/Art/BossFight/HotPotato'
TABLE_GUID = 'b6f6b9db1fb27aa44b50bbcde2da75fe'
CUE_GO, CUE_TR = '6529682720126242077', '3591679651340342855'
TABLE_PARENT = '764810714254100494'
FISH_PADDLE = dict(go='794258876612633996', tr='6110363844157461204', rb='3713051702540464015')
GHOST_PADDLE = dict(go='8972023075688938692', tr='3980294283995937509', rb='7946838754584909760')
SCENE_TABLE_INSTANCE = '1938067402'
SCENE_BOARD = '470086783'
SCENE_MANAGER = '7273385334423750177'
PRIMS = {'sphere': 10207, 'cylinder': 10206, 'cube': 10202}
BLAST_DIAMETER = 3.8          # BossBlast.DefaultRadius * 2
STUN_SECONDS = 1.6            # BossBlast.DefaultStunSeconds
BASE_ID = 5814000000000000000
_next = [BASE_ID]


def new_id():
    _next[0] += 1
    return str(_next[0])


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding='utf-8')


# ------------------------------------------------------------------ scripts and materials
SCRIPTS = ['BossCueBomb', 'BossCueBombView', 'BossFuse', 'BossBlast', 'BossStunState', 'BossDefenseCatalog']


def script_meta(name):
    return (f'fileFormatVersion: 2\nguid: {ak.guid("script/" + name)}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n'
            '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def author_scripts():
    for name in SCRIPTS:
        meta = ROOT / f'Assets/Scripts/BossFight/{name}.cs.meta'
        if not meta.exists():
            write(meta, script_meta(name))


MATERIALS = {   # name -> base colour
    'HP_BombHeat': (1.0, 0.42, 0.06), 'HP_Wick': (0.16, 0.12, 0.09), 'HP_Spark': (1.0, 0.95, 0.55),
    'HP_BlastFlash': (1.0, 0.97, 0.78), 'HP_BlastShock': (1.0, 0.45, 0.08), 'HP_BlastDebris': (0.12, 0.11, 0.11),
    'HP_BlastSmoke': (0.34, 0.35, 0.38), 'HP_StunStar': (1.0, 0.86, 0.12),
}


def author_materials(materials=None):
    materials = materials or MATERIALS
    template = (ROOT / 'Assets/Art/BossFight/BF_CueBall_Ivory.mat').read_text(encoding='utf-8')
    guids = {}
    for name, (r, g, b) in materials.items():
        text = template.replace('m_Name: BF_CueBall_Ivory', f'm_Name: {name}')
        colour = '{r: %s, g: %s, b: %s, a: 1}' % (r, g, b)
        text = re.sub(r'(- _BaseColor: )\{[^}]*\}', r'\g<1>' + colour, text)
        text = re.sub(r'(- _Color: )\{[^}]*\}', r'\g<1>' + colour, text)
        text = text.replace('- _Glossiness: 0.25', '- _Glossiness: 0').replace('- _Smoothness: 0.25', '- _Smoothness: 0')
        write(MATS / f'{name}.mat', text)
        g = ak.guid('material/' + name)
        write(MATS / f'{name}.mat.meta', f'fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
        guids[name] = g
    meta = ROOT / 'Assets/Art/BossFight/HotPotato.meta'
    if not meta.exists():
        write(meta, f'fileFormatVersion: 2\nguid: {ak.guid("folder/HotPotatoMaterials")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return guids


# ------------------------------------------------------------------ clips
def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def hold(path, value, length):
    return {path: [(0.0, value), (length, value)]}


def fuse_clips():
    """Fuse visuals live under 'Cue bomb fuse': Heat shell, Wick, Spark. Burn is one second, slowed to the fuse length."""
    wick, spark = (0.08, 0.28, 0.08), None
    L = 1.0
    idle_scale = {**hold('Heat shell', (0, 0, 0), L), **hold('Wick', (0, 0, 0), L), **hold('Spark', (0, 0, 0), L)}
    idle = ak.clip_yaml('BombFuse_Idle', L, True, scale=idle_scale)
    pulses = 36.0

    def phase(u):
        return math.tau * pulses * (0.35 * u + 0.65 * u * u)

    shell = ak.sampled(lambda u: (lambda s: (s, s, s))(smooth((u - .3) / .25) * (1.0 + .06 * u + (.03 + .05 * u) * math.sin(phase(u)))), L, 380)
    spark_scale = ak.sampled(lambda u: (lambda s: (s, s, s))(.14 + .07 * u + .05 * math.sin(phase(u) * 1.7 + 1)), L, 380)
    spark_pos = ak.sampled(lambda u: (0.0, .98 + .03 * math.sin(phase(u) * 2.3), 0.0), L, 380)
    wick_scale = [(0.0, wick), (L, wick)]
    wick_pos = [(0.0, (0.0, .66, 0.0)), (L, (0.0, .66, 0.0))]
    ramp = lambda u: (1.0, 0.18 + 0.78 * smooth(u), 0.04 + 0.6 * smooth((u - .45) / .55))
    floats = [('Heat shell', f'material._BaseColor.{c}', 23, ak.sampled(lambda u, i=i: (ramp(u)[i],), L, 24)) for i, c in enumerate('rgb')]
    burn = ak.clip_yaml('BombFuse_Burn', L, False, pos={'Spark': spark_pos, 'Wick': wick_pos},
                        scale={'Heat shell': shell, 'Spark': spark_scale, 'Wick': wick_scale}, floats=floats)
    return {'BombFuse_Idle': idle, 'BombFuse_Burn': burn}


def blast_clips():
    L = 0.9
    debris = [(i, math.tau * i / 6 + .4, 1.1 + .35 * (i % 3), .9 + .25 * ((i * 7) % 3)) for i in range(6)]
    smokes = [(j, math.tau * j / 3 + .9, .55 + .12 * j) for j in range(3)]
    idle_scale = {'Flash': [(0.0, (0, 0, 0)), (L, (0, 0, 0))], 'Shock': [(0.0, (0, 0, 0)), (L, (0, 0, 0))]}
    for i, *_ in debris:
        idle_scale[f'Debris {i + 1}'] = [(0.0, (0, 0, 0)), (L, (0, 0, 0))]
    for j, *_ in smokes:
        idle_scale[f'Smoke {j + 1}'] = [(0.0, (0, 0, 0)), (L, (0, 0, 0))]
    idle = ak.clip_yaml('BombBlast_Idle', L, True, scale=idle_scale)
    scale, pos, euler = {}, {}, {}
    scale['Flash'] = ak.sampled(lambda u: (lambda s: (s, s, s))(1.9 * math.sin(math.pi * min(1, u / .3)) if u < .3 else 0.0), L, 30)
    shock_d = lambda u: BLAST_DIAMETER * (1 - (1 - min(1, u / .5)) ** 3) * (1 - smooth((u - .55) / .3))
    scale['Shock'] = ak.sampled(lambda u: (shock_d(u), .03, shock_d(u)), L, 30)
    for i, a, speed, lift in debris:
        name = f'Debris {i + 1}'
        s = lambda u: .13 * (1 - smooth((u - .6) / .4))
        scale[name] = ak.sampled(lambda u, s=s: (s(u), s(u), s(u)), L, 30)
        pos[name] = ak.sampled(lambda u, a=a, speed=speed, lift=lift: (math.cos(a) * speed * 1.5 * u, lift * 2.2 * u - 3.2 * u * u * .9 + .15, math.sin(a) * speed * 1.5 * u), L, 30)
        euler[name] = ak.sampled(lambda u, i=i: (u * 540 * (1 + i % 2), u * 360, u * 720), L, 30)
    for j, a, rise in smokes:
        name = f'Smoke {j + 1}'
        scale[name] = ak.sampled(lambda u: (lambda s: (s, s, s))(.95 * smooth((u - .12) / .5) * (1 - smooth((u - .75) / .25))), L, 30)
        pos[name] = ak.sampled(lambda u, a=a, rise=rise: (math.cos(a) * .5 * u, .25 + rise * u, math.sin(a) * .5 * u), L, 30)
    boom = ak.clip_yaml('BombBlast_Boom', L, False, pos=pos, scale=scale, euler=euler)
    return {'BombBlast_Idle': idle, 'BombBlast_Boom': boom}, debris, smokes


def stun_clips():
    L = STUN_SECONDS
    idle = ak.clip_yaml('StunMarker_Idle', L, True, scale={**hold('Stars', (0, 0, 0), L)})
    scale = ak.sampled(lambda u: (lambda s: (s, s, s))(smooth(u / .08) * (1 - smooth((u - .88) / .12))), L, 30)
    spin = ak.sampled(lambda u: (0.0, u * 1080, 0.0), L, 30)
    stun = ak.clip_yaml('StunMarker_Stun', L, False, scale={'Stars': scale}, euler={'Stars': spin})
    return {'StunMarker_Idle': idle, 'StunMarker_Stun': stun}


def author_animation():
    fuse = fuse_clips()
    blast, debris, smokes = blast_clips()
    stun = stun_clips()
    controllers = {}
    for ctl, clips, idle_name, play_name, returns in (
            ('BombFuse', fuse, 'BombFuse_Idle', 'BombFuse_Burn', ('Burn',)), ('BombBlast', blast, 'BombBlast_Idle', 'BombBlast_Boom', ('Boom',)),
            ('StunMarker', stun, 'StunMarker_Idle', 'StunMarker_Stun', ('Stun',))):
        for name, text in clips.items():
            write(ANIM / f'{name}.anim', text)
            write(ANIM / f'{name}.anim.meta', ak.meta(name, 7400000))
        state = play_name.split('_')[1] if ctl != 'BombFuse' else 'Burn'
        states = [('Idle', ak.guid(idle_name), True), (state, ak.guid(play_name), False)]
        # Burn stays on its last frame (the arena replays or defuses it); Boom and Stun fall back to Idle when finished.
        write(ANIM / f'{ctl}.controller', ak.controller_yaml(ctl, states, 'Idle', returns=() if ctl == 'BombFuse' else (state,)))
        write(ANIM / f'{ctl}.controller.meta', ak.meta(ctl, 9100000))
        controllers[ctl] = ak.guid(ctl)
    meta = ROOT / 'Assets/Animations/BossFight/HotPotato.meta'
    if not meta.exists():
        write(meta, f'fileFormatVersion: 2\nguid: {ak.guid("folder/HotPotatoAnim")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    return controllers, debris, smokes


# ------------------------------------------------------------------ prefab object builders
def vec3(v):
    return '{x: %s, y: %s, z: %s}' % tuple(repr(round(float(c), 5)) if abs(c) > 1e-9 else '0' for c in v)


def quat(rot):
    """rot: None or Euler degrees (x, y, z); written as a quaternion."""
    if not rot:
        return '{x: 0, y: 0, z: 0, w: 1}'
    x, y, z = (math.radians(a) / 2 for a in rot)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    q = (sx * cy * cz + cx * sy * sz, cx * sy * cz - sx * cy * sz, cx * cy * sz + sx * sy * cz, cx * cy * cz - sx * sy * sz)
    return '{x: %s, y: %s, z: %s, w: %s}' % tuple(repr(round(c, 6)) if abs(c) > 1e-9 else '0' for c in q)


def go_doc(fid, name, comps, active=1):
    lines = ''.join(f'  - component: {{fileID: {c}}}\n' for c in comps)
    return (f'--- !u!1 &{fid}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n'
            f'  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n{lines}  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n'
            '  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: {active}\n')


def transform_doc(fid, go, parent, children, pos, scale, rot=None):
    ch = ''.join(f'  - {{fileID: {c}}}\n' for c in children)
    ch = f'  m_Children:\n{ch}' if children else '  m_Children: []\n'
    return (f'--- !u!4 &{fid}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n'
            f'  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  serializedVersion: 2\n  m_LocalRotation: {quat(rot)}\n'
            f'  m_LocalPosition: {vec3(pos)}\n  m_LocalScale: {vec3(scale)}\n  m_ConstrainProportionsScale: 0\n{ch}  m_Father: {{fileID: {parent}}}\n'
            '  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n')


def mesh_docs(go, prim, mat_guid, renderer_template):
    fid_f, fid_r = new_id(), new_id()
    f = (f'--- !u!33 &{fid_f}\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n'
         f'  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Mesh: {{fileID: {PRIMS[prim]}, guid: 0000000000000000e000000000000000, type: 0}}\n')
    body = renderer_template.replace('{GO}', go).replace('{MAT}', mat_guid)
    return fid_f, fid_r, f + f'--- !u!23 &{fid_r}\n' + body


def animator_doc(fid, go, controller_guid):
    return (f'--- !u!95 &{fid}\nAnimator:\n  serializedVersion: 7\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n'
            f'  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_Avatar: {{fileID: 0}}\n'
            f'  m_Controller: {{fileID: 9100000, guid: {controller_guid}, type: 2}}\n  m_CullingMode: 0\n  m_UpdateMode: 2\n  m_ApplyRootMotion: 0\n'
            '  m_LinearVelocityBlending: 0\n  m_StabilizeFeet: 0\n  m_AnimatePhysics: 0\n  m_WarningMessage: \n  m_HasTransformHierarchy: 1\n'
            '  m_AllowConstantClipSamplingOptimization: 1\n  m_KeepAnimatorStateOnDisable: 0\n  m_WriteDefaultValuesOnDisable: 0\n')


def mono_doc(fid, go, script, klass, fields):
    body = ''.join(f'  {k}: {v}\n' for k, v in fields)
    return (f'--- !u!114 &{fid}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n'
            f'  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            f'  m_Script: {{fileID: 11500000, guid: {script if re.fullmatch(r"[0-9a-f]{32}", script) else ak.guid("script/" + script)}, type: 3}}\n  m_Name: \n'
            f'  m_EditorClassIdentifier: Assembly-CSharp::HauntedFish.BossFight.{klass}\n{body}')


class Builder:
    """Collects new documents and the edits they imply on existing ones."""

    def __init__(self, table, renderer_template, mats):
        self.docs, self.table, self.template, self.mats = [], table, renderer_template, mats
        self.children = {}   # parent transform id -> [child transform ids]
        self.specs = []      # transforms are written last, once every node knows its children

    def node(self, name, parent_tr, pos, scale, prim=None, material=None, animator=None, extra_comps=(), active=1, rot=None):
        go, tr = new_id(), new_id()
        comps = [tr]
        extra = []
        if prim:
            fm, mr, doc = mesh_docs(go, prim, self.mats[material], self.template)
            comps += [fm, mr]
            extra.append(doc)
        anim_id = None
        if animator:
            anim_id = new_id()
            comps.append(anim_id)
            extra.append(animator_doc(anim_id, go, animator))
        self.docs.append(go_doc(go, name, comps, active))
        self.specs.append((tr, go, parent_tr, pos, scale, rot))
        self.docs += extra
        self.children.setdefault(parent_tr, []).append(tr)
        return dict(go=go, tr=tr, animator=anim_id)


def build_prefab(guids, controllers, debris, smokes):
    text = TABLE.read_text(encoding='utf-8')
    if 'Cue bomb fuse' in text:
        sys.exit('BossTABLE.prefab already contains the cue bomb; nothing to do.')
    f = uy.File(str(TABLE))
    mr = f.docs['5115464176767574246'].body.rstrip('\n') + '\n'
    mr = re.sub(r'  m_GameObject: \{fileID: \d+\}', '  m_GameObject: {fileID: {GO}}', mr)
    mr = re.sub(r'  m_Materials:\n  - \{[^}]*\}', '  m_Materials:\n  - {fileID: 2100000, guid: {MAT}, type: 2}', mr)
    mr = mr.replace('m_CastShadows: 1', 'm_CastShadows: 0').replace('m_ReceiveShadows: 1', 'm_ReceiveShadows: 0')
    b = Builder(f, mr, guids)
    # fuse visuals ride on the cue ball (local units: the ball's own scale is 0.36, its sphere radius 0.5)
    fuse = b.node('Cue bomb fuse', CUE_TR, (0, 0, 0), (1, 1, 1), animator=controllers['BombFuse'])
    b.node('Heat shell', fuse['tr'], (0, 0, 0), (0, 0, 0), 'sphere', 'HP_BombHeat')
    b.node('Wick', fuse['tr'], (0, .66, 0), (0, 0, 0), 'cylinder', 'HP_Wick')
    b.node('Spark', fuse['tr'], (0, .98, 0), (0, 0, 0), 'sphere', 'HP_Spark')
    # free-standing blast
    blast = b.node('Cue bomb blast', TABLE_PARENT, (1000, 1.62, 0), (1, 1, 1), animator=controllers['BombBlast'])
    b.node('Flash', blast['tr'], (0, 0, 0), (0, 0, 0), 'sphere', 'HP_BlastFlash')
    b.node('Shock', blast['tr'], (0, 0, 0), (0, 0, 0), 'cylinder', 'HP_BlastShock')
    for i, *_ in debris:
        b.node(f'Debris {i + 1}', blast['tr'], (0, 0, 0), (0, 0, 0), 'cube', 'HP_BlastDebris')
    for j, *_ in smokes:
        b.node(f'Smoke {j + 1}', blast['tr'], (0, 0, 0), (0, 0, 0), 'sphere', 'HP_BlastSmoke')
    # stun markers above each paddle
    stuns = {}
    for label, paddle in (('Fish', FISH_PADDLE), ('Ghost', GHOST_PADDLE)):
        marker = b.node(f'{label} stun marker', paddle['tr'], (0, .95, 0), (1, 1, 1), animator=controllers['StunMarker'])
        stars = b.node('Stars', marker['tr'], (0, 0, 0), (0, 0, 0))
        for k in range(3):
            a = math.tau * k / 3
            b.node(f'Star {k + 1}', stars['tr'], (math.cos(a) * .38, 0, math.sin(a) * .38), (.17, .17, .17), 'sphere', 'HP_StunStar')
        stuns[label] = marker
    # components on the cue ball
    bomb_id, view_id = new_id(), new_id()
    bomb = mono_doc(bomb_id, CUE_GO, 'BossCueBomb', 'BossCueBomb', [
        ('_Defenses', '{fileID: 0}'), ('_FishPaddle', f'{{fileID: {FISH_PADDLE["rb"]}}}'), ('_GhostPaddle', f'{{fileID: {GHOST_PADDLE["rb"]}}}'),
        ('_FuseSeconds', '9'), ('_MinRemaining', '1.5'), ('_StrikePenalty', '0.6')])
    view = mono_doc(view_id, CUE_GO, 'BossCueBombView', 'BossCueBombView', [
        ('_Fuse', f'{{fileID: {fuse["animator"]}}}'), ('_Blast', f'{{fileID: {blast["animator"]}}}'), ('_FishStun', f'{{fileID: {stuns["Fish"]["animator"]}}}'),
        ('_GhostStun', f'{{fileID: {stuns["Ghost"]["animator"]}}}'), ('_BlastRoot', f'{{fileID: {blast["tr"]}}}')])
    b.docs += [transform_doc(tr, go, parent, b.children.get(tr, []), pos, scale, rot) for tr, go, parent, pos, scale, rot in b.specs]
    b.docs += [bomb, view]
    # splice: children lists, cue-ball component list, then append new documents
    def add_children(doc_id, ids):
        nonlocal text
        pat = re.compile(r'(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:)( \[\]\n|\n(?:  - \{fileID: -?\d+\}\n)+)' % doc_id)
        m = pat.search(text)
        assert m, doc_id
        existing = '' if m.group(2).strip() == '[]' else m.group(2)
        new = existing if existing else '\n'
        new += ''.join(f'  - {{fileID: {c}}}\n' for c in ids)
        text = text[:m.start(2)] + new + text[m.end(2):]
    for parent, ids in b.children.items():
        if parent in {CUE_TR, TABLE_PARENT, FISH_PADDLE['tr'], GHOST_PADDLE['tr']}:
            add_children(parent, ids)
    m = re.search(r'(--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n(?:  - component: \{fileID: -?\d+\}\n)+)' % CUE_GO, text)
    text = text.replace(m.group(1), m.group(1) + f'  - component: {{fileID: {bomb_id}}}\n  - component: {{fileID: {view_id}}}\n', 1)
    text = text.rstrip('\n') + '\n' + ''.join(b.docs)
    TABLE.write_text(text, encoding='utf-8')
    return bomb_id, view_id


def build_scene(bomb_id, view_id):
    text = SCENE.read_text(encoding='utf-8')
    if f'propertyPath: _Defenses\n' in text and bomb_id in text:
        sys.exit('Scene already wired.')
    override = (f'    - target: {{fileID: {bomb_id}, guid: {TABLE_GUID}, type: 3}}\n      propertyPath: _Defenses\n      value: \n'
                f'      objectReference: {{fileID: {SCENE_BOARD}}}\n')
    m = re.search(r'(--- !u!1001 &%s\nPrefabInstance:\n(?:.*\n)*?)(    m_RemovedComponents:)' % SCENE_TABLE_INSTANCE, text)
    assert m, 'table instance'
    text = text[:m.end(1)] + override + text[m.end(1):]
    sid_bomb, sid_view = new_id(), new_id()
    stripped = ''
    for sid, comp, script in ((sid_bomb, bomb_id, 'BossCueBomb'), (sid_view, view_id, 'BossCueBombView')):
        stripped += (f'--- !u!114 &{sid} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: {comp}, guid: {TABLE_GUID}, type: 3}}\n'
                     f'  m_PrefabInstance: {{fileID: {SCENE_TABLE_INSTANCE}}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n'
                     f'  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {ak.guid("script/" + script)}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n')
    marker = '--- !u!1660057539 &9223372036854775807\n'
    assert marker in text
    text = text.replace(marker, stripped + marker, 1)
    a = '  _DragInput: {fileID: 470086782}\n'
    manager = re.search(r'--- !u!114 &%s\nMonoBehaviour:\n(?:.*\n)*?%s' % (SCENE_MANAGER, re.escape(a)), text)
    assert manager, 'manager _DragInput'
    text = text[:manager.end()] + f'  _Bomb: {{fileID: {sid_bomb}}}\n  _BombView: {{fileID: {sid_view}}}\n' + text[manager.end():]
    SCENE.write_text(text, encoding='utf-8')


def main():
    author_scripts()
    guids = author_materials()
    controllers, debris, smokes = author_animation()
    bomb_id, view_id = build_prefab(guids, controllers, debris, smokes)
    build_scene(bomb_id, view_id)
    print('cue bomb authored: bomb component', bomb_id, 'view component', view_id)


if __name__ == '__main__':
    main()
