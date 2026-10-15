"""Reads the boss tentacle sprites and finds each shoulder (where the tentacle meets the body).

The segmented boss is a head plus eight full-canvas tentacle sprites that all pivot at their canvas centre, so a
rotation of the Transform swings the arm around the wrong point. Clips therefore rotate around the shoulder: the
Transform's rotation is paired with a position offset that keeps the shoulder fixed.
"""
import json
import math
import re
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / 'Assets/Art/Boss'
PPU = 512.0
def shoulder(path):
    """Shoulder in sprite-local units (x right, y up) relative to the canvas centre."""
    im = Image.open(path).convert('RGBA')
    a = np.asarray(im.getchannel('A')) > 40
    ys, xs = np.nonzero(a)
    # the base is where the tentacle touches the canvas edge nearest the body: use the lowest ~4% of its rows
    bottom = ys.max()
    rows = ys >= bottom - max(6, int((bottom - ys.min()) * .04))
    cx = xs[rows].mean()
    cy = bottom
    w, h = im.size
    return ((cx - w / 2) / PPU, (h / 2 - cy) / PPU)


def vec3(text):
    return [float(v) for v in re.findall(r'[xyz]: ([\-\d.e]+)', text)][:3]


def quat4(text):
    return [float(v) for v in re.findall(r'[xyzw]: ([\-\d.e]+)', text)][:4]


PREFAB = ROOT / 'Assets/Resources/BossFight/BossSegmentedOpponent.prefab'
VARIANTS = {1: 'v1 ', 2: 'v2 ', 3: 'v3 '}


SCENE = ROOT / 'Assets/Scenes/BossFight.unity'
CHARACTERS = ROOT / 'Assets/Resources/BossFight/SceneObjects/BossCHARACTERS.prefab'
INSTANCE_IN_SCENE = '718528073'


def scene_overrides():
    """The scene overrides most tentacle positions/scales/rotations of the nested rig. Returns {transform_id: {property: value}}.

    Nested-prefab override targets are the source object's fileID XOR the nested PrefabInstance's fileID."""
    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import unity_yaml as uy
    nested = [k for k in uy.File(str(CHARACTERS)).order if uy.File(str(CHARACTERS)).docs[k].ty == '1001'][0]
    mask = 0xFFFFFFFFFFFFFFFF
    x = int(nested) & mask
    text = SCENE.read_text(encoding='utf-8', errors='replace')
    body = re.search(r'--- !u!1001 &%s\nPrefabInstance:\n(.*?)(?=\n--- !u!)' % INSTANCE_IN_SCENE, text, re.S).group(1)
    out = {}
    for tid, prop, val in re.findall(r'- target: \{fileID: (-?\d+), guid: \w+, type: 3\}\n      propertyPath: ([^\n]+)\n      value: ([^\n]*)', body):
        out.setdefault((int(tid) & mask) ^ x, {})[prop] = val
    return out


def effective(tid, overrides, pos, scale, rot):
    """Apply scene overrides (keyed by transform id) to prefab position/scale/rotation. Returns pos(3), scale(3), z-degrees."""
    o = overrides.get(int(tid) & 0xFFFFFFFFFFFFFFFF, {})
    p = [float(o.get(f'm_LocalPosition.{a}', pos[i])) for i, a in enumerate('xyz')]
    sc = [float(o.get(f'm_LocalScale.{a}', scale[i])) for i, a in enumerate('xyz')]
    q = [float(o.get(f'm_LocalRotation.{a}', rot[i])) for i, a in enumerate('xyzw')]
    return p, sc, math.degrees(2 * math.atan2(q[2], q[3]))


def load(variant=1):
    """Per-variant rest positions, sprites and sort order straight from the authored prefab, plus shoulders from the art."""
    import glob
    import sys
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import unity_yaml as uy
    guid2png = {}
    for m in glob.glob(str(ART / '*.png.meta')):
        guid2png[re.search(r'guid: (\w+)', open(m).read()).group(1)] = m[:-5]
    OVER = scene_overrides()
    f = uy.File(str(PREFAB))
    go = [k for k in f.order if f.docs[k].ty == '1' and f.docs[k].name and f.docs[k].name.startswith(VARIANTS[variant])][0]
    root = [c for c in f.comps(go) if f.docs[c].ty == '4'][0]
    arms = {}
    head = None
    for c in f.children(root):
        d = f.docs[c]
        g = f.docs[d.ref('m_GameObject')]
        sr = [x for x in f.comps(g.fid) if f.docs[x].ty == '212'][0]
        guid = re.search(r'm_Sprite: \{fileID: -?\d+, guid: (\w+)', f.docs[sr].body).group(1)
        order = int(re.search(r'm_SortingOrder: (-?\d+)', f.docs[sr].body).group(1))
        x, y = re.search(r'x: ([\-\d.e]+), y: ([\-\d.e]+)', d.get('m_LocalPosition')).groups()
        path = Path(guid2png[guid])
        if g.name == 'Head':
            pz = float(re.search(r'z: ([\-\d.e]+)', d.get('m_LocalPosition')).group(1))
            p, sc, rz = effective(c, OVER, (float(x), float(y), pz), vec3(d.get('m_LocalScale')), quat4(d.get('m_LocalRotation')))
            head = dict(path=path, rest=(p[0], p[1]), rest_z=p[2], scale=(sc[0], sc[1]), rest_rot=rz, order=order)
            continue
        sx, sy = shoulder(path)
        pz = float(re.search(r'z: ([\-\d.e]+)', d.get('m_LocalPosition')).group(1))
        p, sc, rz = effective(c, OVER, (float(x), float(y), pz), vec3(d.get('m_LocalScale')), quat4(d.get('m_LocalRotation')))
        arms[g.name] = dict(path=path, rest=(p[0], p[1]), rest_z=p[2], scale=(sc[0], sc[1]), rest_rot=rz, shoulder=(sx, sy), order=order, transform=c, go=g.fid)
    return arms, head, dict(root=go, transform=root)


if __name__ == '__main__':
    for v in (1, 2, 3):
        arms, head, info = load(v)
        print('variant', v, 'head', head['path'].name)
        for n, a in arms.items():
            print('  ', n, a['path'].name, 'rest', a['rest'], 'shoulder', tuple(round(float(q), 2) for q in a['shoulder']))
