"""Generic Unity AnimationClip / AnimatorController YAML writers used by the hot-potato authoring."""
import hashlib
import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ArmAnimation'))
from author_arm_clips import float_curve, keys, num, vec, vector_curve  # noqa: E402


def guid(name):
    return hashlib.md5(('HotPotato/' + name).encode()).hexdigest()


def sampled(fn, length, rate):
    n = max(2, int(round(length * rate)))
    return [(length * i / n, fn(i / n)) for i in range(n + 1)]


def clip_yaml(name, length, loop, pos=None, scale=None, euler=None, floats=None):
    """pos/scale/euler: {path: [(t, (x, y, z))]};  floats: [(path, attribute, classID, [(t, (v,))])]."""
    pos, scale, euler, floats = pos or {}, scale or {}, euler or {}, floats or []
    out = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!74 &7400000', 'AnimationClip:', '  m_ObjectHideFlags: 0',
           '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', f'  m_Name: {name}',
           '  serializedVersion: 7', '  m_Legacy: 0', '  m_Compressed: 0', '  m_UseHighQualityCurve: 1', '  m_RotationCurves: []',
           '  m_CompressedRotationCurves: []']

    def block(title, table):
        lines = [f'  {title}:'] + ([] if table else [])
        if not table:
            return [f'  {title}: []']
        for path, series in table.items():
            ts = [t for t, _ in series]
            ks = keys(ts, [v for _, v in series], False)
            lines.append(vector_curve(path, ks))
        return lines

    out += block('m_EulerCurves', euler) + block('m_PositionCurves', pos) + block('m_ScaleCurves', scale)
    if floats:
        out.append('  m_FloatCurves:')
        for path, attr, cid, series in floats:
            ks = keys([t for t, _ in series], [v for _, v in series], False)
            lines = ['  - serializedVersion: 2', '    curve:', '      serializedVersion: 2', '      m_Curve:']
            for t, v, s in ks:
                lines += ['      - serializedVersion: 3', f'        time: {num(t)}', f'        value: {num(v[0])}', f'        inSlope: {num(s[0])}', f'        outSlope: {num(s[0])}',
                          '        tangentMode: 0', '        weightedMode: 0', '        inWeight: 0.33333334', '        outWeight: 0.33333334']
            lines += ['      m_PreInfinity: 2', '      m_PostInfinity: 2', '      m_RotationOrder: 4', f'    attribute: {attr}', f'    path: {path}', f'    classID: {cid}', '    script: {fileID: 0}']
            out.append('\n'.join(lines))
    else:
        out.append('  m_FloatCurves: []')
    out += ['  m_PPtrCurves: []', '  m_SampleRate: 30', '  m_WrapMode: 0', '  m_Bounds:', '    m_Center: {x: 0, y: 0, z: 0}', '    m_Extent: {x: 0, y: 0, z: 0}',
            '  m_ClipBindingConstant:', '    genericBindings: []', '    pptrCurveMapping: []', '  m_AnimationClipSettings:', '    serializedVersion: 2',
            '    m_AdditiveReferencePoseClip: {fileID: 0}', '    m_AdditiveReferencePoseTime: 0', '    m_StartTime: 0', f'    m_StopTime: {num(length)}',
            '    m_OrientationOffsetY: 0', '    m_Level: 0', '    m_CycleOffset: 0', '    m_HasAdditiveReferencePose: 0', f'    m_LoopTime: {1 if loop else 0}',
            '    m_LoopBlend: 0', '    m_LoopBlendOrientation: 0', '    m_LoopBlendPositionY: 0', '    m_LoopBlendPositionXZ: 0', '    m_KeepOriginalOrientation: 0',
            '    m_KeepOriginalPositionY: 1', '    m_KeepOriginalPositionXZ: 0', '    m_HeightFromFeet: 0', '    m_Mirror: 0', '  m_EditorCurves:']
    edit = []
    for table, prefix in ((pos, 'm_LocalPosition'), (scale, 'm_LocalScale')):
        for path, series in table.items():
            ks = keys([t for t, _ in series], [v for _, v in series], False)
            for axis, label in enumerate('xyz'):
                edit.append(float_curve(path, f'{prefix}.{label}', ks, axis))
    for path, series, _ in [(p, s, 0) for p, s in euler.items()]:
        pass
    for path, attr, cid, series in floats:
        ks = keys([t for t, _ in series], [v for _, v in series], False)
        edit.append(float_curve(path, attr, ks, 0).replace('classID: 4', f'classID: {cid}'))
    out.append('\n'.join(edit) if edit else '')
    out.append('  m_EulerEditorCurves:')
    eedit = []
    for path, series in euler.items():
        ks = keys([t for t, _ in series], [v for _, v in series], False)
        for axis, label in enumerate('xyz'):
            eedit.append(float_curve(path, f'localEulerAnglesRaw.{label}', ks, axis))
    out.append('\n'.join(eedit) if eedit else '')
    out += ['  m_HasGenericRootTransform: 0', '  m_HasMotionFloatCurves: 0', '  m_Events: []']
    text = '\n'.join(l for l in out if l != '') + '\n'
    return text.replace('  m_EulerEditorCurves:\n  m_HasGenericRootTransform', '  m_EulerEditorCurves: []\n  m_HasGenericRootTransform')


def controller_yaml(name, states, default, returns=()):
    """states: list of (state_name, clip_guid, loop). States named in `returns` exit back to `default` at the clip's end."""
    sm, ctl = 1107000, 9100000
    ids = {s: 1102000 + i for i, (s, _, _) in enumerate(states)}
    trans = {s: 1101000 + i for i, s in enumerate(returns)}
    out = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', f'--- !u!91 &{ctl}', 'AnimatorController:', '  m_ObjectHideFlags: 0',
           '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', f'  m_Name: {name}',
           '  serializedVersion: 5', '  m_AnimatorParameters: []', '  m_AnimatorLayers:', '  - serializedVersion: 5', '    m_Name: Base Layer',
           f'    m_StateMachine: {{fileID: {sm}}}', '    m_Mask: {fileID: 0}', '    m_Motions: []', '    m_Behaviours: []', '    m_BlendingMode: 0',
           '    m_SyncedLayerIndex: -1', '    m_DefaultWeight: 0', '    m_IKPass: 0', '    m_SyncedLayerAffectsTiming: 0', f'    m_Controller: {{fileID: {ctl}}}']
    for i, (state, clip_guid, _) in enumerate(states):
        trs = f'\n  - {{fileID: {trans[state]}}}' if state in trans else ' []'
        out += [f'--- !u!1102 &{ids[state]}', 'AnimatorState:', '  serializedVersion: 6', '  m_ObjectHideFlags: 1', '  m_CorrespondingSourceObject: {fileID: 0}',
                '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', f'  m_Name: {state}', '  m_Speed: 1', '  m_CycleOffset: 0', f'  m_Transitions:{trs}',
                '  m_StateMachineBehaviours: []', f'  m_Position: {{x: 220, y: {40 + 60 * i}, z: 0}}', '  m_IKOnFeet: 0', '  m_WriteDefaultValues: 1', '  m_Mirror: 0',
                '  m_SpeedParameterActive: 0', '  m_MirrorParameterActive: 0', '  m_CycleOffsetParameterActive: 0', '  m_TimeParameterActive: 0',
                f'  m_Motion: {{fileID: 7400000, guid: {clip_guid}, type: 2}}', '  m_Tag: ', '  m_SpeedParameter: ', '  m_MirrorParameter: ', '  m_CycleOffsetParameter: ', '  m_TimeParameter: ']
    for state, tid in trans.items():
        out += [f'--- !u!1101 &{tid}', 'AnimatorStateTransition:', '  m_ObjectHideFlags: 1', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
                '  m_PrefabAsset: {fileID: 0}', '  m_Name: ', '  m_Conditions: []', '  m_DstStateMachine: {fileID: 0}', f'  m_DstState: {{fileID: {ids[default]}}}',
                '  m_Solo: 0', '  m_Mute: 0', '  m_IsExit: 0', '  serializedVersion: 3', '  m_TransitionDuration: 0', '  m_TransitionOffset: 0', '  m_ExitTime: 1',
                '  m_HasExitTime: 1', '  m_HasFixedDuration: 1', '  m_InterruptionSource: 0', '  m_OrderedInterruption: 1', '  m_CanTransitionToSelf: 1']
    out += [f'--- !u!1107 &{sm}', 'AnimatorStateMachine:', '  serializedVersion: 6', '  m_ObjectHideFlags: 1', '  m_CorrespondingSourceObject: {fileID: 0}',
            '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', '  m_Name: Base Layer', '  m_ChildStates:']
    for i, (state, _, _) in enumerate(states):
        out += ['  - serializedVersion: 1', f'    m_State: {{fileID: {ids[state]}}}', f'    m_Position: {{x: 220, y: {40 + 60 * i}, z: 0}}']
    out += ['  m_ChildStateMachines: []', '  m_AnyStateTransitions: []', '  m_EntryTransitions: []', '  m_StateMachineTransitions: {}', '  m_StateMachineBehaviours: []',
            '  m_AnyStatePosition: {x: 50, y: 20, z: 0}', '  m_EntryPosition: {x: 50, y: 120, z: 0}', '  m_ExitPosition: {x: 800, y: 120, z: 0}',
            '  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}', f'  m_DefaultState: {{fileID: {ids[default]}}}']
    return '\n'.join(out) + '\n'


def meta(name, main_id):
    return (f'fileFormatVersion: 2\nguid: {guid(name)}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {main_id}\n'
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
