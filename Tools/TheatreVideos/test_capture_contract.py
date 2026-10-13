import json
from pathlib import Path
import tempfile
import unittest

from capture_contract import ROLES, sha256, validated_captures


class CaptureContractTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.plan_path = self.root / 'plan.json'
        self.manifest_path = self.root / 'capture.json'
        self.plan = {
            'revision': 'test-only',
            'presentation': {'opening_titles': {'Fish': 'Guide to all Fish', 'Ghost': 'Guide to the Ghost'}},
            'required_headings_before_publish': {'Fish': ['Guide to all Fish', 'Wowza'], 'Ghost': ['Guide to the Ghost', 'Wowza']},
            'required_capture_checks': {role: ['real-observation'] for role in ROLES},
        }
        self.manifest = {'schema_version': 1, 'plan_revision': 'test-only', 'captures': {}}
        for role in ROLES:
            ink = self.ink(role)
            ink.parent.mkdir(parents=True, exist_ok=True)
            opening = self.plan['presentation']['opening_titles'][role]
            ink.write_text(f'~ TutorialCaption("{opening}", "Example")\n~ Wait(4.0)\n~ TutorialCaption("Wowza", "")\n')
            # Contract-only fixture bytes, never passed to FFmpeg or represented as video.
            source = self.root / f'{role}.fixture'
            source.write_bytes(b'non-video test fixture')
            evidence = self.root / f'{role}-observations.txt'
            evidence.write_text('PASS real-observation\n')
            self.manifest['captures'][role] = {
                'completed': True, 'native_scene': 'Assets/Scenes/TutorialRecording.unity',
                'source': source.name, 'source_sha256': sha256(source), 'ink_sha256': sha256(ink),
                'observations': evidence.name, 'observations_sha256': sha256(evidence),
            }

    def ink(self, role):
        return self.root / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink'

    def validate(self):
        self.plan_path.write_text(json.dumps(self.plan))
        self.manifest_path.write_text(json.dumps(self.manifest))
        return validated_captures(self.root, self.manifest_path, self.plan_path)

    def test_matching_contract(self):
        self.assertEqual(set(self.validate()), set(ROLES))

    def test_reject_stale_plan(self):
        self.manifest['plan_revision'] = 'old'
        with self.assertRaisesRegex(ValueError, 'current recording plan'):
            self.validate()

    def test_reject_unfinished_native_capture(self):
        self.manifest['captures']['Ghost']['completed'] = False
        with self.assertRaisesRegex(ValueError, 'completed real'):
            self.validate()

    def test_reject_non_native_source(self):
        self.manifest['captures']['Fish']['native_scene'] = 'mock'
        with self.assertRaisesRegex(ValueError, 'real authored Unity'):
            self.validate()

    def test_reject_changed_ink(self):
        with self.ink('Fish').open('a') as stream:
            stream.write('~ Wait(1.0)\n')
        with self.assertRaisesRegex(ValueError, 'recorded Ink differs'):
            self.validate()

    def test_reject_changed_source(self):
        (self.root / 'Ghost.fixture').write_bytes(b'changed bytes')
        with self.assertRaisesRegex(ValueError, 'source hash'):
            self.validate()

    def test_reject_changed_observations(self):
        (self.root / 'Ghost-observations.txt').write_text('modified')
        with self.assertRaisesRegex(ValueError, 'observation log or its hash'):
            self.validate()

    def test_reject_missing_required_observation(self):
        evidence = self.root / 'Ghost-observations.txt'
        evidence.write_text('PASS some-other-check\n')
        self.manifest['captures']['Ghost']['observations_sha256'] = sha256(evidence)
        with self.assertRaisesRegex(ValueError, 'native observations still required'):
            self.validate()

    def test_reject_repeated_opening(self):
        ink = self.ink('Fish')
        with ink.open('a') as stream:
            stream.write('~ TutorialCaption("Guide to all Fish", "Again")\n')
        self.manifest['captures']['Fish']['ink_sha256'] = sha256(ink)
        with self.assertRaisesRegex(ValueError, 'exactly once'):
            self.validate()

    def test_reject_pending_chapters(self):
        self.plan['required_headings_before_publish']['Fish'].append('Angler')
        with self.assertRaisesRegex(ValueError, 'pending guide chapters: Angler'):
            self.validate()

    def test_checkout_plan_still_pending(self):
        plan = json.loads(Path(__file__).with_name('recording-plan.json').read_text())
        self.assertEqual(plan['status'], 'pending_native_import_and_capture')
        self.assertEqual(plan['presentation']['death_card'], 'Wowza')
        self.assertFalse(plan['presentation']['repeated_corner_labels'])
        self.assertEqual(plan['runtime_contracts']['angler']['prefab'], 'Assets/Resources/Traps/AnglerFishTrap.prefab')


if __name__ == '__main__':
    unittest.main()
