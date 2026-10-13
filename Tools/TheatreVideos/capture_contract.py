"""Reject stale/incomplete recording sources before silent-guide rendering."""
from pathlib import Path
import hashlib
import json
import re

ROLES = ('Fish', 'Ghost')


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def instructions(path):
    steps = []
    for line in Path(path).read_text(encoding='utf-8-sig').splitlines():
        match = re.fullmatch(r'~ (TutorialShot|TutorialCaption|TutorialDemo|TutorialSetup|Wait)\((.*)\)', line.strip())
        if match:
            steps.append((match[1], json.loads('[' + match[2] + ']')))
    return steps


def validated_captures(root, manifest_path, plan_path):
    """The native recorder/reviewer supplies evidence; this function never fabricates it."""
    root, manifest_path, plan_path = Path(root), Path(manifest_path), Path(plan_path)
    plan = json.loads(plan_path.read_text(encoding='utf-8'))
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    if manifest.get('schema_version') != 1 or manifest.get('plan_revision') != plan['revision']:
        raise ValueError('Capture manifest does not match the current recording plan.')
    captures = {}
    for role in ROLES:
        capture = manifest.get('captures', {}).get(role, {})
        if capture.get('completed') is not True or capture.get('native_scene') != 'Assets/Scenes/TutorialRecording.unity':
            raise ValueError(f'{role}: a completed real authored Unity scene capture is required.')
        ink = root / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink'
        if capture.get('ink_sha256') != sha256(ink):
            raise ValueError(f'{role}: recorded Ink differs from current Ink; do not pair new cards with old footage.')
        steps = instructions(ink)
        headings = [values[0] for kind, values in steps if kind == 'TutorialCaption']
        opening = plan['presentation']['opening_titles'][role]
        if not headings or headings[0] != opening or headings.count(opening) != 1:
            raise ValueError(f'{role}: the opening title must appear exactly once, on the first card.')
        missing = set(plan['required_headings_before_publish'][role]) - set(headings)
        if missing:
            raise ValueError(f'{role}: pending guide chapters: {", ".join(sorted(missing))}.')
        source = manifest_path.parent / capture.get('source', '')
        evidence = manifest_path.parent / capture.get('observations', '')
        if not source.is_file() or capture.get('source_sha256') != sha256(source):
            raise ValueError(f'{role}: real footage file or source hash is missing or mismatched.')
        if not evidence.is_file() or capture.get('observations_sha256') != sha256(evidence):
            raise ValueError(f'{role}: native observation log or its hash is missing or mismatched.')
        passed = set(evidence.read_text(encoding='utf-8').splitlines())
        missing_checks = [check for check in plan['required_capture_checks'][role] if 'PASS ' + check not in passed]
        if missing_checks:
            raise ValueError(f'{role}: native observations still required: {", ".join(missing_checks)}.')
        captures[role] = {'source': source, 'steps': steps}
    return captures
