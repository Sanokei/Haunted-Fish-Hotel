"""Small parser regressions; run with Python's standard unittest discovery."""
import math
import contextlib
import io
import unittest
from unittest.mock import patch
from pathlib import Path
import tempfile
import verify_scene_assets as validator
from verify_scene_assets import reference_issues, records, fit_distance, rotate, mesh_issues, AssetGraph, in_unity_ignored_directory


class ExactLightingPreservationTests(unittest.TestCase):
    path = 'Assets/Scenes/Lobby.unity'

    def test_directional_light_uses_only_its_authored_bias_override(self):
        original = '  m_UsePipelineSettings: 1\n  m_ShadowResolutionTier: 2\n'
        current = original.replace('m_UsePipelineSettings: 1','m_UsePipelineSettings: 0')
        normalize = validator.preserved_record_body
        self.assertEqual(normalize(self.path,1998747292,current),original)
        self.assertNotEqual(normalize(self.path,1998747292,current.replace('Tier: 2','Tier: 0')),original)
        self.assertEqual(normalize(self.path,1998747291,current),current)

    def test_black_clear_normalizes_only_the_approved_camera_and_values(self):
        current = ('  m_ClearFlags: 2\n  m_BackGroundColor: {r: 0, g: 0, b: 0, a: 1}\n'
                   '  field of view: 60\n')
        original = ('  m_ClearFlags: 1\n'
                    '  m_BackGroundColor: {r: 0.19215687, g: 0.3019608, b: 0.4745098, a: 0}\n'
                    '  field of view: 60\n')
        normalize = validator.preserved_record_body
        self.assertEqual(normalize(self.path,330585545,current),original)
        self.assertNotEqual(normalize(self.path,330585545,current.replace('view: 60','view: 55')),original)
        self.assertEqual(normalize(self.path,330585545,current.replace('r: 0,','r: 0.1,')),current.replace('r: 0,','r: 0.1,'))
        self.assertEqual(normalize(self.path,330585546,current),current)
        self.assertEqual(normalize('Assets/Scenes/Game.unity',330585545,current),current)

    def test_shadow_key_rotation_keeps_position_scale_and_hierarchy_protected(self):
        current = ('  m_LocalRotation: {x: 0.408217894, y: -0.234569716, z: 0.109381655, w: 0.875426098}\n'
                   '  m_LocalPosition: {x: 0, y: 0, z: 0}\n'
                   '  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_Father: {fileID: 0}\n'
                   '  m_LocalEulerAnglesHint: {x: 50, y: -30, z: 0}\n')
        original = current.replace('0.408217894, y: -0.234569716, z: 0.109381655, w: 0.875426098',
                                   '0, y: 0, z: 0, w: 1').replace('x: 50, y: -30','x: 0, y: 0')
        normalize = validator.preserved_record_body
        self.assertEqual(normalize(self.path,1998747294,current),original)
        for old,new in (('m_LocalPosition: {x: 0','m_LocalPosition: {x: 1'),
                        ('m_LocalScale: {x: 1','m_LocalScale: {x: 2'),
                        ('m_Father: {fileID: 0}','m_Father: {fileID: 10}'),
                        ('0.408217894','0.5')):
            self.assertNotEqual(normalize(self.path,1998747294,current.replace(old,new)),original)
        self.assertEqual(normalize(self.path,1998747293,current),current)


class UnityIgnoredDirectoryTests(unittest.TestCase):
    def test_only_tilde_suffixed_directory_contents_are_ignored(self):
        for path in ('Assets/Art/Source~/PiranhaStun.blend',
                     'Assets/Art/Source~/nested/mesh_report.json',
                     'Assets/Art/Source~/nested/mesh.asset.meta',
                     'Packages/example/Samples~/sample.prefab'):
            with self.subTest(path=path):
                self.assertTrue(in_unity_ignored_directory(path))
        for path in ('Assets/Art/Source/PiranhaStun.blend',
                     'Assets/Art/Source~copy/mesh.asset',
                     'Assets/Art/Piranha~Stun.asset',
                     'Assets/Art/PiranhaStun.asset~',
                     'Assets/Art/mesh_report.json'):
            with self.subTest(path=path):
                self.assertFalse(in_unity_ignored_directory(path))

    def run_validation(self, files, changed, explicit=()):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for path, text in files.items():
                target = root / path
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_text(text)
            def fake_git(*args):
                return ('\0'.join(changed) + '\0').encode() if args[0] == 'diff' else b''
            argv = ['verify_scene_assets.py']
            for path in explicit:
                argv.extend(('--asset', path))
            output = io.StringIO()
            with patch.object(validator, 'ROOT', root), patch.object(validator, 'DEFAULT_ASSETS', ()), \
                    patch.object(validator, 'git', side_effect=fake_git), \
                    patch.object(validator, 'baseline', return_value=''), \
                    patch.object(validator, 'existed_meta', return_value=False), \
                    patch.object(validator, 'existed_reference', return_value=False), \
                    patch.object(validator, 'check_preserved_records'), \
                    patch.object(validator, 'check_presentation_contracts'), \
                    patch.object(validator, 'check_lobby_finishes'), \
                    patch.object(validator, 'check_cameras'), \
                    patch('sys.argv', argv), contextlib.redirect_stdout(output):
                result = validator.main()
            return result, output.getvalue()

    def test_ignored_sources_need_no_metadata_even_when_explicitly_selected(self):
        source = 'Assets/Art/Source~/PiranhaStun.blend'
        result, output = self.run_validation({source: 'source-only'}, [source], [source])
        self.assertEqual(result, 0, output)
        self.assertNotIn('metadata is paired', output)

    def test_imported_assets_still_require_metadata(self):
        for source in ('Assets/Art/PiranhaStun.blend', 'Assets/Art/Source~copy/mesh_report.json'):
            with self.subTest(source=source):
                result, output = self.run_validation({source: 'imported asset'}, [source])
                self.assertEqual(result, 1)
                self.assertIn(source + ': metadata is paired', output)

    def test_ignored_metadata_is_not_a_duplicate_imported_guid(self):
        guid = 'a' * 32
        source = 'Assets/Art/mesh.asset'
        files = {source: 'imported asset', source + '.meta': f'guid: {guid}\n',
                 'Assets/Art/Source~/mesh.asset': 'ignored source',
                 'Assets/Art/Source~/mesh.asset.meta': f'guid: {guid}\n'}
        result, output = self.run_validation(files, list(files))
        self.assertEqual(result, 0, output)

    def test_ignored_metadata_cannot_resolve_an_imported_asset_reference(self):
        source, target_guid = 'Assets/Art/use.asset', 'b' * 32
        files = {
            source: '%YAML 1.1\n--- !u!114 &1\nMonoBehaviour:\n'
                    f'  target: {{fileID: 2, guid: {target_guid}, type: 2}}\n',
            source + '.meta': 'guid: ' + 'a' * 32 + '\n',
            'Assets/Art/Source~/target.asset': 'ignored source',
            'Assets/Art/Source~/target.asset.meta': f'guid: {target_guid}\n',
        }
        result, output = self.run_validation(files, list(files))
        self.assertEqual(result, 1)
        self.assertIn('newly unresolved GUID ' + target_guid, output)


class SerializedReferenceTests(unittest.TestCase):
    def test_signed_ids_and_stripped_references_resolve(self):
        source = ('--- !u!4 &-42 stripped\nTransform:\n'
                  '  m_CorrespondingSourceObject: {fileID: 77, guid: ' + 'a'*32 + ', type: 3}\n'
                  '  m_PrefabInstance: {fileID: 2}\n'
                  '--- !u!1001 &2\nPrefabInstance:\n  parent: {fileID: -42}\n')
        self.assertEqual(reference_issues(source), set())
        self.assertTrue(records(source)[0].stripped)

    def test_external_file_id_is_not_a_local_reference(self):
        source = '--- !u!114 &1\nMonoBehaviour:\n  asset: {fileID: 44, guid: ' + 'b'*32 + ', type: 2}\n'
        self.assertEqual(reference_issues(source), set())

    def test_missing_local_reference_is_detected(self):
        self.assertEqual(reference_issues('--- !u!114 &1\nMonoBehaviour:\n  target: {fileID: 42}\n'),
                         {'unresolved local fileID 42'})

    def test_duplicate_id_is_detected(self):
        self.assertIn('duplicate local fileID 1', reference_issues(
            '--- !u!4 &1\nTransform:\n--- !u!4 &1\nTransform:\n'))

    def test_component_ownership_is_detected(self):
        source = ('--- !u!1 &1\nGameObject:\n  m_Component:\n  - component: {fileID: 2}\n'
                  '--- !u!4 &2\nTransform:\n  m_GameObject: {fileID: 2}\n')
        self.assertIn('component 2 belongs to a different GameObject than 1', reference_issues(source))

    def test_identity_and_half_turn_rotation(self):
        self.assertEqual(rotate((0,0,0,1), (1,2,3)), (1,2,3))
        self.assertEqual(rotate((0,1,0,0), (1,2,3)), (-1,2,-3))

    def test_portrait_fit_is_farther_than_landscape(self):
        args = ((7.3,.7,11.6), -.05, math.radians(35), 55)
        self.assertGreater(fit_distance(*args, 9/16, .1), fit_distance(*args, 16/9, .1))

    def test_fitted_near_corner_has_padding(self):
        pitch, tan = math.radians(35), math.tan(math.radians(55/2))
        distance = fit_distance((7.3,.7,11.6), -.05, pitch, 55, 9/16, .1)
        for y in (-.4,.3):
            for z in (-5.8,5.8):
                depth = distance-y*math.sin(pitch)+z*math.cos(pitch)
                self.assertGreater(depth, .1)
                self.assertLessEqual(abs(3.65/(depth*tan*9/16)), .92001)
                self.assertLessEqual(abs((y*math.cos(pitch)+z*math.sin(pitch))/(depth*tan)), .92001)

    def test_mesh_rejects_out_of_range_indices_and_truncated_vertices(self):
        source = ('--- !u!43 &43\nMesh:\n  m_IndexFormat: 0\n  m_IndexBuffer: 000001000200\n'
                  '  m_VertexData:\n    m_VertexCount: 2\n    m_DataSize: 8\n    _typelessdata: 00000000\n')
        self.assertEqual(mesh_issues(source), {'mesh 43: vertex buffer byte count mismatch',
                                               'mesh 43: index outside vertex buffer'})

    def test_stripped_camera_inherits_scene_numeric_overrides(self):
        guid = 'a' * 32
        prefab = ('--- !u!4 &1\nTransform:\n  m_GameObject: {fileID: 10}\n'
                  '  m_LocalPosition: {x: 5, y: 6, z: 7}\n'
                  '  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n'
                  '  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_Father: {fileID: 0}\n'
                  '--- !u!20 &2\nCamera:\n  m_GameObject: {fileID: 10}\n  field of view: 55\n')
        scene = ('--- !u!1001 &100\nPrefabInstance:\n  m_Modification:\n'
                 '    m_TransformParent: {fileID: 0}\n    m_Modifications:\n'
                 f'    - target: {{fileID: 1, guid: {guid}, type: 3}}\n'
                 '      propertyPath: m_LocalPosition.y\n      value: 42\n      objectReference: {fileID: 0}\n'
                 f'    - target: {{fileID: 2, guid: {guid}, type: 3}}\n'
                 '      propertyPath: field of view\n      value: 60\n      objectReference: {fileID: 0}\n'
                 '--- !u!20 &200 stripped\nCamera:\n'
                 f'  m_CorrespondingSourceObject: {{fileID: 2, guid: {guid}, type: 3}}\n'
                 '  m_PrefabInstance: {fileID: 100}\n')
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path/'source.prefab').write_text(prefab)
            (path/'scene.unity').write_text(scene)
            camera = AssetGraph(path/'scene.unity', {guid:path/'source.prefab'}).node(200)
            self.assertEqual(camera.read('field of view'), '60')
            self.assertEqual(camera.transform().pose()[0], (5,42,7))


if __name__ == '__main__':
    unittest.main()
