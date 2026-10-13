"""Small parser regressions; run with Python's standard unittest discovery."""
import math
import unittest
from pathlib import Path
import tempfile
from verify_scene_assets import reference_issues, records, fit_distance, rotate, mesh_issues, AssetGraph


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
