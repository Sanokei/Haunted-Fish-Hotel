#!/usr/bin/env python3
"""Source-only checks for the Lobby/Boss presentation patch; no Unity import required."""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
from dataclasses import dataclass
import itertools
from functools import lru_cache
import math
from pathlib import Path
import re
import subprocess
import struct

try:
    import yaml
except ImportError:
    yaml = None

ROOT = Path(__file__).resolve().parents[2]
DOC = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?\r?\n(.*?)(?=^--- !u!|\Z)', re.M | re.S)
REFERENCE = re.compile(r'\{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}), type: \d+)?\}')
GUID = re.compile(r'^guid: ([0-9a-f]{32})$', re.M)
OVERRIDE = re.compile(r'    - target: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d+\}\n'
                      r'      propertyPath: (.+)\n      value:([^\n]*)\n      objectReference: ([^\n]+)')
DEFAULT_ASSETS = ('Assets/Scenes/Lobby.unity', 'Assets/Scenes/BossFight.unity',
                  'Assets/Resources/BossFight/SceneObjects/BossVIEWS.prefab',
                  'Assets/Prefabs/LobbyAtmosphere/LobbyDollyAtmosphere.prefab')
BUILTIN_GUIDS = {'00000000000000000000000000000000',
                 '0000000000000000e000000000000000', '0000000000000000f000000000000000'}


def git(*args):
    return subprocess.run(['git', '-C', str(ROOT), *args], check=True,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout


def in_unity_ignored_directory(path):
    """Unity excludes contents beneath directories whose names end in '~'.

    Check directory components only: a '~' elsewhere in an asset filename or
    directory name must not suppress metadata/reference validation.
    """
    return any(part.endswith('~') for part in Path(path).parts[:-1])


@lru_cache(maxsize=None)
def baseline(path, base):
    result = subprocess.run(['git', '-C', str(ROOT), 'show', f'{base}:{path}'],
                            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    return result.stdout.decode('utf-8-sig', errors='replace') if result.returncode == 0 else ''


@dataclass
class Record:
    kind: int
    ident: int
    stripped: bool
    body: str


@lru_cache(maxsize=1024)
def records(source):
    return [Record(int(m[1]), int(m[2]), bool(m[3]), m[4]) for m in DOC.finditer(source)]


def value(body, field):
    match = re.search(r'^  ' + re.escape(field) + r': ?([^\n]*)$', body, re.M)
    if not match:
        raise ValueError(f'missing field {field}')
    return match[1].strip()


def local_ref(body, field):
    return int(REFERENCE.fullmatch(value(body, field))[1])


def vector(raw):
    return tuple(float(v) for _, v in re.findall(r'([xyzw]): ([^,}]+)', raw))


def syntax_issues(source):
    if yaml is None:
        return set()
    issues = set()
    for doc in records(source):
        try:
            yaml.load(doc.body, Loader=getattr(yaml, 'CSafeLoader', yaml.SafeLoader))
        except yaml.YAMLError as error:
            issues.add(f'YAML object {doc.ident}: {error.problem}')
    return issues


def mesh_issues(source):
    issues = set()
    for doc in records(source):
        if doc.kind != 43:
            continue
        count = int(re.search(r'm_VertexCount: (\d+)', doc.body)[1])
        data = re.search(r'_typelessdata: ([a-fA-F0-9]*)', doc.body)
        size = int(re.search(r'm_DataSize: (\d+)', doc.body)[1])
        if not data or len(data[1]) != 2 * size:
            issues.add(f'mesh {doc.ident}: vertex buffer byte count mismatch')
        indices = bytes.fromhex(value(doc.body, 'm_IndexBuffer'))
        width, format_code = (2, 'H') if value(doc.body, 'm_IndexFormat') == '0' else (4, 'I')
        if len(indices) % width:
            issues.add(f'mesh {doc.ident}: truncated index buffer')
        elif any(i[0] >= count for i in struct.iter_unpack('<' + format_code, indices)):
            issues.add(f'mesh {doc.ident}: index outside vertex buffer')
        for offset, length in re.findall(r'    firstByte: (\d+)\n    indexCount: (\d+)', doc.body):
            if int(offset) + int(length) * width > len(indices):
                issues.add(f'mesh {doc.ident}: submesh exceeds index buffer')
    return issues


def reference_issues(source):
    docs = records(source)
    counts = Counter(d.ident for d in docs)
    issues = {f'duplicate local fileID {ident}' for ident, count in counts.items() if count > 1}
    for match in REFERENCE.finditer(source):
        ident, guid = int(match[1]), match[2]
        if ident and guid is None and ident not in counts:
            issues.add(f'unresolved local fileID {ident}')
    by_id = {d.ident: d for d in docs}
    for doc in docs:
        if doc.kind != 1 or doc.stripped:
            continue
        component_ids = [int(v) for v in re.findall(r'component: \{fileID: (-?\d+)\}', doc.body)]
        if len(component_ids) != len(set(component_ids)):
            issues.add(f'GameObject {doc.ident} repeats a component')
        transforms = [i for i in component_ids if i in by_id and by_id[i].kind in (4, 224)]
        if len(transforms) != 1:
            issues.add(f'GameObject {doc.ident} has {len(transforms)} authored transforms')
        for ident in component_ids:
            component = by_id.get(ident)
            if component and not component.stripped:
                if local_ref(component.body, 'm_GameObject') != doc.ident:
                    issues.add(f'component {ident} belongs to a different GameObject than {doc.ident}')
    for doc in docs:
        if doc.kind not in (4, 224) or doc.stripped:
            continue
        children = re.search(r'^  m_Children:([^\n]*)(\n(?:  -[^\n]*\n)*)?', doc.body, re.M)
        ids = [int(x) for x in re.findall(r'fileID: (-?\d+)', children[0] if children else '')]
        if len(ids) != len(set(ids)):
            issues.add(f'transform {doc.ident} repeats a child')
        for ident in ids:
            child = by_id.get(ident)
            if child and not child.stripped and local_ref(child.body, 'm_Father') != doc.ident:
                issues.add(f'transform {doc.ident} lists child {ident} with a different parent')
    return issues


class Report:
    def __init__(self):
        self.checks = 0
        self.errors = []
        self.notes = []

    def check(self, condition, label):
        self.checks += 1
        if not condition:
            self.errors.append(label)

    def regressions(self, current, previous, label):
        for issue in sorted(current - previous):
            self.check(False, f'{label}: {issue}')
        self.check(not (current - previous), f'{label}: no new structural/reference issues')
        if current & previous:
            self.notes.append(f'{label}: {len(current & previous)} existing issue(s), not introduced by this patch')


class AssetGraph:
    """Resolve authored local/stripped nodes and the numeric overrides used by these rigs."""
    def __init__(self, path, guid_paths, overrides=None, parent=None):
        self.path, self.guid_paths = path, guid_paths
        self.docs = {d.ident: d for d in records(path.read_text(encoding='utf-8-sig'))}
        self.overrides = overrides or {}
        self.parent = parent

    def node(self, ident):
        doc = self.docs[ident]
        if not doc.stripped:
            return Node(self, doc)
        source = REFERENCE.fullmatch(value(doc.body, 'm_CorrespondingSourceObject'))
        instance = self.docs[local_ref(doc.body, 'm_PrefabInstance')]
        overrides = {(int(m[1]), m[3]): (m[4].strip(), m[5])
                     for m in OVERRIDE.finditer(instance.body) if m[2] == source[2]}
        parent_id = int(re.search(r'm_TransformParent: \{fileID: (-?\d+)\}', instance.body)[1])
        source_graph = AssetGraph(self.guid_paths[source[2]], self.guid_paths, overrides,
                                  self.node(parent_id) if parent_id else None)
        return source_graph.node(int(source[1]))


class Node:
    def __init__(self, graph, doc):
        self.graph, self.doc = graph, doc

    def read(self, field):
        override = self.graph.overrides.get((self.doc.ident, field))
        return override[0] if override is not None else value(self.doc.body, field)

    def ref(self, field):
        return self.graph.node(local_ref(self.doc.body, field))

    def vec(self, field):
        raw = vector(value(self.doc.body, field))
        return tuple(float(self.graph.overrides.get((self.doc.ident, field + '.' + axis), (v, ''))[0])
                     for axis, v in zip('xyzw', raw))

    def transform(self):
        if self.doc.kind in (4, 224):
            return self
        go = local_ref(self.doc.body, 'm_GameObject')
        return self.graph.node(next(d.ident for d in self.graph.docs.values()
                                    if d.kind in (4, 224) and not d.stripped
                                    and local_ref(d.body, 'm_GameObject') == go))

    def pose(self, visited=None):
        visited = set() if visited is None else visited
        key = (str(self.graph.path), self.doc.ident)
        if key in visited:
            raise ValueError('transform cycle at ' + str(key))
        visited.add(key)
        position, rotation, scale = self.vec('m_LocalPosition'), self.vec('m_LocalRotation'), self.vec('m_LocalScale')
        parent_id = local_ref(self.doc.body, 'm_Father')
        parent = self.graph.node(parent_id) if parent_id else self.graph.parent
        if parent:
            p, q, s = parent.pose(visited)
            position = add(p, rotate(q, tuple(x * y for x, y in zip(position, s))))
            rotation = multiply(q, rotation)
            scale = tuple(x * y for x, y in zip(scale, s))
        return position, rotation, scale


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def multiply(a, b):
    x, y, z, w = a
    X, Y, Z, W = b
    return (w*X+x*W+y*Z-z*Y, w*Y-x*Z+y*W+z*X, w*Z+x*Y-y*X+z*W, w*W-x*X-y*Y-z*Z)


def rotate(q, v):
    length = math.sqrt(dot(q, q))
    q = tuple(x / length for x in q)
    return multiply(multiply(q, (*v, 0)), (-q[0], -q[1], -q[2], q[3]))[:3]


def fit_distance(size, centre_y, pitch, fov, aspect, near):
    """Double-precision transcription of RequiredDistance, not execution of C# source."""
    sin, cos, tan = math.sin(pitch), math.cos(pitch), math.tan(math.radians(fov / 2))
    required = 1.0
    for y, z in itertools.product((centre_y-size[1]/2, centre_y+size[1]/2), (-size[2]/2, size[2]/2)):
        depth, up = -y*sin+z*cos, y*cos+z*sin
        required = max(required, size[0]/2/(tan*aspect*.92)-depth, abs(up)/(tan*.92)-depth, near+.3-depth)
    return required


def check_cameras(report, guid_paths):
    graph = AssetGraph(ROOT / 'Assets/Scenes/BossFight.unity', guid_paths)
    views = [d for d in graph.docs.values() if '::HauntedFish.BossFight.BossArenaView\n' in d.body]
    report.check(len(views) == 1, 'BossFight has one authored BossArenaView')
    if len(views) != 1:
        return
    view = Node(graph, views[0])
    focus = view.ref('_Focus').pose()[0]
    def authored_markers(field):
        marker_match = re.search(r'^  ' + field + r':([^\n]*)(\n(?:  -[^\n]*\n)*)?', view.doc.body, re.M)
        ids = [int(x) for x in re.findall(r'fileID: (-?\d+)', marker_match[0] if marker_match else '')]
        return [tuple(x-y for x, y in zip(graph.node(i).pose()[0], focus)) for i in ids if i]
    shared_markers = authored_markers('_AuthoredFrameMarkers')
    fish_markers = authored_markers('_FishViewFrameMarkers')
    for side in ('Fish', 'Ghost', 'Spectator'):
        camera = view.ref('_' + side)
        markers = shared_markers + (fish_markers if side != 'Ghost' else [])
        report.check(camera.doc.kind == 20 and camera.read('orthographic') == '0', side + ' camera is authored perspective')
        _, rotation, scale = camera.transform().pose()
        right, up, forward = (rotate(rotation, v) for v in ((1,0,0), (0,1,0), (0,0,1)))
        report.check(abs(forward[0]) < 1e-4 and abs(right[1]) < 1e-4, side + ' camera matches the no-roll own-end fit model')
        report.check(all(abs(s-1) < 1e-4 for s in scale), side + ' camera has unit world scale')
        report.check(forward[2] < 0 if side == 'Ghost' else forward[2] > 0, side + ' camera looks from its own end')
        pitch = math.asin(-forward[1])
        fov, near = float(camera.read('field of view')), float(camera.read('near clip plane'))
        report.check(10 <= math.degrees(pitch) <= 75 and 20 <= fov <= 100 and near > 0, side + ' fit inputs in supported range')
        prefix = '_SpectatorFrame' if side == 'Spectator' else '_Frame'
        size, centre = view.vec(prefix + 'Size'), float(view.read(prefix + 'CentreY'))
        report.check(size[0] >= 7.3 and size[2] >= 11.6 and centre-size[1]/2 <= -.4+1e-5
                     and centre+size[1]/2 >= .3-1e-5, side + ' authored envelope covers playable board and goals')
        viewport = re.search(r'  m_NormalizedViewPortRect:\n(.*?)(?=^  \S)', camera.doc.body, re.M | re.S)[1]
        def viewport_value(name):
            authored = re.search(r'^    ' + name + r': ([^\n]+)', viewport, re.M)[1]
            return float(camera.graph.overrides.get((camera.doc.ident, 'm_NormalizedViewPortRect.'+name), (authored, ''))[0])
        width, height = viewport_value('width'), viewport_value('height')
        report.check(width > 0 and height > 0, side + ' camera viewport has positive dimensions')
        for screen_aspect in (9/16, 3/4, 1, 4/3, 16/9, 21/9, 32/9):
            aspect = screen_aspect * width / height
            distance = fit_distance(size, centre, pitch, fov, aspect, near)
            tan = math.tan(math.radians(fov/2))
            for marker in markers:
                distance = max(distance, abs(dot(marker,right))/(tan*aspect*.92)-dot(marker,forward),
                               abs(dot(marker,up))/(tan*.92)-dot(marker,forward), near+.3-dot(marker,forward))
            far = max(100, distance + math.sqrt(dot(size,size)) + 5)
            board = list(itertools.product((-3.65,3.65), (-.4,.3), (-5.8,5.8)))
            board += list(itertools.product((-size[0]/2,size[0]/2),
                                            (centre-size[1]/2,centre+size[1]/2), (-size[2]/2,size[2]/2)))
            for point in board + markers:
                depth = distance + dot(point, forward)
                report.check(near < depth < far and abs(dot(point,right)/(depth*tan*aspect)) <= .92001
                             and abs(dot(point,up)/(depth*tan)) <= .92001,
                             f'{side} aspect {screen_aspect:.4f}: board/goal/marker corner in fitted frustum')
    report.notes.append('Camera checks use serialized scene/prefab overrides and a Python math transcription; they do not prove Unity rendering or occlusion.')


@lru_cache(maxsize=None)
def existed_reference(guid, base):
    result = subprocess.run(['git', '-C', str(ROOT), 'grep', '-q', '-F', 'guid: ' + guid,
                             base, '--', 'Assets'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return result.returncode == 0


@lru_cache(maxsize=None)
def existed_meta(guid, base):
    result = subprocess.run(['git', '-C', str(ROOT), 'grep', '-q', '-F', 'guid: ' + guid,
                             base, '--', '*.meta'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return result.returncode == 0


def check_preserved_records(report, base):
    physics = {50, 54, 56, 58, 60, 61, 64, 65, 135, 136, 143}
    scopes = {'Assets/Scenes/Lobby.unity': physics | {4, 20, 81, 82, 114, 223, 224, 225},
              'Assets/Scenes/BossFight.unity': physics,
              'Assets/Resources/BossFight/SceneObjects/BossTABLE.prefab': physics | {4},
              'Assets/Resources/BossFight/SceneObjects/BossDEFENSES.prefab': physics | {4},
              'Assets/Resources/BossFight/SceneObjects/BossROOM.prefab': physics,
              'Assets/Resources/BossFight/SceneObjects/BossCHARACTERS.prefab': physics | {4}}
    for path, kinds in scopes.items():
        current = {d.ident: d for d in records((ROOT/path).read_text(encoding='utf-8-sig'))}
        for doc in records(baseline(path, base)):
            if path.endswith('/BossCHARACTERS.prefab') and doc.ident == 764626834192637407:
                continue  # The decorative fish sprite alone is resized; gameplay/reaction pivots stay authored.
            if doc.kind in kinds:
                report.check(doc.ident in current and current[doc.ident].body == doc.body,
                             f'{path}: protected authored object {doc.ident} (class {doc.kind}) preserved')


def check_presentation_contracts(report):
    storm = (ROOT/'Assets/Scripts/BossFight/BossStormPresentation.cs').read_text()
    report.check(bool(re.search(r'if\s*\(\s*_StageLights\s*==\s*null\s*\)\s*return', storm)),
                 'stage lights are optional and null-safe')
    report.check(bool(re.search(r'void Awake\(\)\s*=>\s*SetStageLights\(false\)', storm)),
                 'stage lights start hidden in Awake')
    report.check(storm.index('SetStageLights(watching)') > storm.index('_Lightning.ValidateAuthoredBindings'),
                 'stage light activation follows authored storm validation')
    disabled = re.search(r'void OnDisable\(\)(.*?)(?=^        }|^    })', storm, re.M | re.S)
    report.check(disabled is not None and 'SetStageLights(false)' in disabled[1],
                 'stage lights shut down on disable')
    view = (ROOT/'Assets/Scripts/BossFight/BossArenaView.cs').read_text()
    report.check(bool(re.search(r'if\s*\(\s*_Side\s*!=\s*BossViewerSide.Ghost\s*\)\s*\{?\s*'
                               r'FitMarkers\(camera,\s*_FishViewFrameMarkers,\s*ref distance\)', view)),
                 'raised opponent markers apply to Fish and Spectator, not hidden local Ghost')
    scene = records((ROOT/'Assets/Scenes/BossFight.unity').read_text())
    by_id = {d.ident:d for d in scene}
    owner = next(d for d in scene if '::HauntedFish.BossFight.BossStormPresentation\n' in d.body)
    binding = re.search(r'^  _StageLights:\n((?:  -[^\n]*\n)+)', owner.body, re.M)
    ids = [int(x) for x in re.findall(r'fileID: (-?\d+)', binding[1] if binding else '')]
    report.check(len(ids) > 0 and len(ids) == len(set(ids)), 'distinct authored stage lights are bound')
    for ident in ids:
        obj = by_id.get(ident)
        components = re.findall(r'component: \{fileID: (-?\d+)\}', obj.body if obj else '')
        report.check(obj is not None and obj.kind == 1 and
                     any(by_id[int(i)].kind == 108 for i in components), f'stage object {ident} owns an authored Light')


def check_lobby_finishes(report):
    path = ROOT/'Assets/Prefabs/LobbyAtmosphere/LobbySurfaceFinishes.prefab'
    if not path.exists():
        return
    docs = records(path.read_text())
    report.check(all(d.kind in (1,4,23,33) for d in docs), 'lobby finishes contain only authored mesh presentation')
    report.check(sum(d.kind == 23 for d in docs) == 2, 'lobby finishes have one wallpaper and one carpet renderer')
    mesh = (ROOT/'Assets/Art/Materials/LobbyRevision/LobbyCarpetSurface.asset').read_text()
    vertex_count = int(re.search(r'm_VertexCount: (\d+)', mesh)[1])
    data = bytes.fromhex(re.search(r'_typelessdata: ([a-fA-F0-9]+)', mesh)[1])
    stride = len(data) // vertex_count
    vertices = [struct.unpack_from('<fff', data, i*stride) for i in range(vertex_count)]
    indices = [i[0] for i in struct.iter_unpack('<H', bytes.fromhex(value(records(mesh)[0].body, 'm_IndexBuffer')))]
    report.check(len(indices) % 3 == 0, 'carpet index count contains complete triangles')
    for i in range(0, len(indices), 3):
        a,b,c = (vertices[index] for index in indices[i:i+3])
        winding_y = (b[2]-a[2])*(c[0]-a[0]) - (b[0]-a[0])*(c[2]-a[2])
        report.check(winding_y > 0, f'carpet triangle {i//3} faces up')
    heights = [p[1] for p in vertices]
    report.check(max(heights)-min(heights) < 1e-6, 'carpet is planar')
    storm = {d.ident:d for d in records((ROOT/'Assets/Prefabs/LobbyAtmosphere/LobbyWindowStormVisuals.prefab').read_text())}
    for ident in (3335016230688513328,6008031163044671315):
        report.check(abs(vector(value(storm[ident].body,'m_LocalPosition'))[1]-heights[0]) < 1e-6,
                     f'floor lightning pass {ident} matches raised carpet height')
    normal_meta = (ROOT/'Assets/Art/LobbyRevisionTextures/Lobby_Carpet_Weave_Normal.png.meta').read_text()
    report.check('  textureType: 1\n' in normal_meta and '    sRGBTexture: 0\n' in normal_meta,
                 'carpet normal uses a linear normal-map importer')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base', default='HEAD', help='Git revision to compare (use parent revision after committing)')
    parser.add_argument('--asset', action='append', default=[], help='Additional repository-relative asset to inspect')
    args = parser.parse_args()
    git('rev-parse', '--verify', args.base)
    changed = set(git('diff', '--name-only', '-z', args.base, '--', 'Assets').decode().split('\0'))
    changed.update(git('ls-files', '--others', '--exclude-standard', '-z', '--', 'Assets').decode().split('\0'))
    changed.discard('')
    changed = {path for path in changed if not in_unity_ignored_directory(path)}
    paths = set(DEFAULT_ASSETS) | set(args.asset) | changed
    paths = {path for path in paths if not in_unity_ignored_directory(path)}
    report = Report()
    guid_entries = defaultdict(list)
    for folder in ('Assets', 'Packages', 'Library/PackageCache'):
        for meta in (ROOT / folder).rglob('*.meta') if (ROOT / folder).exists() else ():
            if in_unity_ignored_directory(meta.relative_to(ROOT)):
                continue
            match = GUID.search(meta.read_text(encoding='utf-8-sig', errors='replace'))
            if match:
                guid_entries[match[1]].append(meta.with_suffix(''))
    guid_paths = {g: p[0] for g, p in guid_entries.items()}
    unresolved = set()
    inspected = 0
    for path in sorted(paths):
        absolute = ROOT / path
        if not absolute.is_file() or path.endswith('.meta'):
            continue
        old = baseline(path, args.base)
        if path in changed:
            report.check(absolute.with_name(absolute.name+'.meta').is_file(), path + ': metadata is paired')
        try:
            source = absolute.read_text(encoding='utf-8-sig')
        except UnicodeDecodeError:
            continue
        if not source.startswith('%YAML'):
            if path.startswith('Assets/Scripts/') and path.endswith('.cs'):
                pattern = r'new\s+GameObject\s*\(|\bAddComponent\s*[<(]|\bCreatePrimitive\s*\('
                report.check(len(re.findall(pattern, source)) <= len(re.findall(pattern, old)), path + ': no introduced runtime object construction')
            continue
        inspected += 1
        report.regressions(reference_issues(source) | syntax_issues(source) | mesh_issues(source),
                           reference_issues(old) | syntax_issues(old) | mesh_issues(old), path)
        old_docs = {d.ident: d for d in records(old)}
        for doc in records(source):
            previous = old_docs.get(doc.ident)
            if doc.kind == 114 and previous and previous.kind == 114:
                report.check(value(doc.body, 'm_Script') == value(previous.body, 'm_Script'),
                             f'{path}: existing component {doc.ident} keeps its script identity')
        old_refs = {(m[1], m[2]) for m in REFERENCE.finditer(old)}
        for match in REFERENCE.finditer(source):
            ident, guid = int(match[1]), match[2]
            if guid is None or guid in BUILTIN_GUIDS or ident == 0:
                continue
            target = guid_paths.get(guid)
            if target is not None and not target.exists():
                report.check(False, f'{path}: GUID {guid} has metadata but no asset')
            if target is None:
                unresolved.add(guid)
                report.check(not existed_meta(guid, args.base) and ((match[1], guid) in old_refs or existed_reference(guid, args.base)),
                             f'{path}: newly unresolved GUID {guid}')
            elif target.suffix in ('.prefab', '.unity', '.asset', '.mat', '.controller', '.anim') and target.is_file():
                target_text = target.read_text(encoding='utf-8-sig', errors='replace')
                if target_text.startswith('%YAML'):
                    target_ids = {d.ident for d in records(target_text)}
                    valid = ident in target_ids or (target.suffix == '.prefab' and ident == 100100000)
                    # Only inherited unresolved subassets are tolerated; deleted target objects are regressions.
                    target_old_ids = {d.ident for d in records(baseline(target.relative_to(ROOT).as_posix(), args.base))}
                    inherited = (match[1], guid) in old_refs and ident not in target_old_ids
                    report.check(valid or inherited, f'{path}: unresolved fileID {ident} in {target.relative_to(ROOT)}')
    for path in sorted(changed):
        if not path.endswith('.meta') or not (ROOT/path).exists():
            continue
        source = (ROOT/path).read_text(encoding='utf-8-sig')
        guid, old_guid = GUID.search(source), GUID.search(baseline(path, args.base))
        report.check(bool(guid), path + ': valid metadata GUID')
        if guid:
            report.check(len(guid_entries[guid[1]]) == 1, path + ': no duplicate metadata GUID')
            report.check(old_guid is None or old_guid[1] == guid[1], path + ': existing asset identity preserved')
        report.check((ROOT/path).with_suffix('').exists(), path + ': metadata has an asset or folder')
    check_preserved_records(report, args.base)
    check_presentation_contracts(report)
    check_lobby_finishes(report)
    try:
        check_cameras(report, guid_paths)
    except (ValueError, KeyError, StopIteration, TypeError, AttributeError) as error:
        report.check(False, 'camera graph could not be fully verified: ' + str(error))
    for error in report.errors:
        print('FAIL:', error)
    print(f'{"FAIL" if report.errors else "PASS"}: {report.checks} assertions, {inspected} YAML assets, {len(changed)} changed asset paths against {args.base}.')
    print(f'NOTE: {len(unresolved)} pre-existing externally unresolved GUIDs encountered; source-only checks do not validate unavailable package imports.')
    for note in report.notes:
        print('NOTE:', note)
    print('YAML syntax: ' + ('checked with installed PyYAML' if yaml else 'not checked; optional PyYAML is absent'))
    print('NOT RUN: Unity import/compilation, rendering, physics, or connected multiplayer.')
    return 1 if report.errors else 0


if __name__ == '__main__':
    raise SystemExit(main())
