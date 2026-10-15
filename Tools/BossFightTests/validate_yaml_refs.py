#!/usr/bin/env python3
"""Static integrity checks for hand-authored Unity YAML (scenes and prefabs). Not a substitute for opening Unity.

    python Tools/BossFightTests/validate_yaml_refs.py [files...]

Checks per file: unique document ids; every local {fileID: n} resolves (stripped docs count); Transform parent/child
links are symmetric; each GameObject's component list points at documents that name it back; external GUIDs exist as
.meta files; and PrefabInstance override / added-component targets exist in their source prefab.
"""
import glob
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ['Assets/Scenes/BossFight.unity', 'Assets/Resources/BossFight/SceneObjects/BossTABLE.prefab',
           'Assets/Resources/BossFight/SceneObjects/BossDEFENSES.prefab', 'Assets/Resources/BossFight/BossSegmentedOpponent.prefab']
BUILTIN = {'0000000000000000e000000000000000', '0000000000000000f000000000000000'}


def load(path):
    text = Path(path).read_text(encoding='utf-8', errors='replace')
    parts = re.split(r'(?m)^(--- !u!\d+ &-?\d+[^\n]*)\n', text)
    docs = {}
    ids = []
    for i in range(1, len(parts), 2):
        m = re.match(r'--- !u!(\d+) &(-?\d+)( stripped)?', parts[i])
        docs[m.group(2)] = (int(m.group(1)), parts[i + 1], bool(m.group(3)))
        ids.append(m.group(2))
    return docs, ids


_guid_cache = {}


def known_guids():
    if not _guid_cache:
        for meta in glob.glob(str(ROOT / 'Assets/**/*.meta'), recursive=True) + glob.glob(str(ROOT / 'Packages/**/*.meta'), recursive=True):
            try:
                m = re.search(r'^guid: (\w{32})', Path(meta).read_text(errors='replace'), re.M)
            except OSError:
                continue
            if m:
                _guid_cache[m.group(1)] = meta[:-5]
    return _guid_cache


def check(path):
    errors = []
    docs, ids = load(path)
    if len(ids) != len(set(ids)):
        errors.append('duplicate document ids: ' + ', '.join(sorted({i for i in ids if ids.count(i) > 1})[:5]))
    guids = known_guids()
    for fid, (ty, body, stripped) in docs.items():
        for m in re.finditer(r'\{fileID: (-?\d+)(?:, guid: (\w+), type: \d)?\}', body):
            ref, guid = m.group(1), m.group(2)
            if ref == '0':
                continue
            if guid is None:
                if ref not in docs and not (ty in (1001,) and False):
                    errors.append(f'&{fid}: unresolved local reference {ref}')
            elif guid not in BUILTIN and guid not in guids:
                errors.append(f'&{fid}: unknown external guid {guid}')
    # transform symmetry
    for fid, (ty, body, stripped) in docs.items():
        if ty != 4 or stripped:
            continue
        father = re.search(r'm_Father: \{fileID: (-?\d+)\}', body).group(1)
        if father != '0' and father in docs and not docs[father][2]:
            kids = re.findall(r'\{fileID: (-?\d+)\}', docs[father][1].split('m_Children:')[1].split('m_Father')[0]) if 'm_Children:' in docs[father][1] else []
            if fid not in kids:
                errors.append(f'&{fid}: parent {father} does not list it as a child')
        if 'm_Children:' in body:
            for c in re.findall(r'\{fileID: (-?\d+)\}', body.split('m_Children:')[1].split('m_Father')[0]):
                if c in docs and not docs[c][2] and f'm_Father: {{fileID: {fid}}}' not in docs[c][1]:
                    errors.append(f'&{fid}: child {c} does not name it as father')
    # gameobject <-> component links
    for fid, (ty, body, stripped) in docs.items():
        if ty != 1 or stripped:
            continue
        for c in re.findall(r'component: \{fileID: (-?\d+)\}', body):
            if c not in docs:
                errors.append(f'GameObject &{fid}: missing component {c}')
            elif not docs[c][2] and f'm_GameObject: {{fileID: {fid}}}' not in docs[c][1]:
                errors.append(f'GameObject &{fid}: component {c} names another owner')
    # prefab override targets exist in their source prefab
    sources = {}
    for fid, (ty, body, stripped) in docs.items():
        if ty != 1001:
            continue
        src = re.search(r'm_SourcePrefab: \{fileID: \d+, guid: (\w+)', body).group(1)
        if src not in guids:
            errors.append(f'PrefabInstance &{fid}: unknown source prefab {src}')
            continue
        if src not in sources:
            sources[src] = load(guids[src])[0] if guids[src].endswith('.prefab') else {}
        for m in re.finditer(r'target(?:CorrespondingSourceObject)?: \{fileID: (-?\d+), guid: (\w+)', body):
            if m.group(2) == src and sources[src] and m.group(1) not in sources[src]:
                errors.append(f'PrefabInstance &{fid}: override target {m.group(1)} missing from {Path(guids[src]).name}')
    return errors


def main():
    files = sys.argv[1:] or DEFAULT
    bad = 0
    for f in files:
        errs = check(f if Path(f).is_absolute() else ROOT / f)
        print(('FAIL ' if errs else 'ok   ') + f, f'({len(errs)} problems)' if errs else '')
        for e in errs[:25]:
            print('   ', e)
        bad += bool(errs)
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
