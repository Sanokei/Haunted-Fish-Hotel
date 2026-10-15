#!/usr/bin/env python3
"""Writes the boss arm AnimationClips and per-variant AnimatorControllers as Unity YAML.

    python author_arm_clips.py

Output: Assets/Animations/BossFight/Arms/BossArms_v{1,2,3}_<State>.anim and BossArms_v{1,2,3}.controller (+ .meta).
GUIDs are derived from the file names, so regenerating never changes an asset's identity. Curves are sampled at
30 fps from poses.py. Paths are relative to each variant's root GameObject ("Head", "Left tentacle 1" ...), so one
controller per variant keeps variant 3's slightly different rest positions exact.
Not validated in the Unity editor: import the folder and scrub each clip before trusting it.
"""
import hashlib
import math
from pathlib import Path

import poses
import rig_data

ROOT = rig_data.ROOT
OUT = ROOT / 'Assets/Animations/BossFight/Arms'
FPS = 30   # clip sample rate; keys are far sparser and Hermite-smoothed
KEY_RATE = {'Idle': 8, 'Juggle': 12, 'Panic': 24, 'Frustrated': 16, 'Gloat': 16, 'Flinch': 20, 'Defeat': 8, 'Triumph': 16}
STATE_ORDER = ['Idle', 'Juggle', 'Panic', 'Frustrated', 'Gloat', 'Flinch', 'Defeat', 'Triumph']
ONE_SHOT_RETURNS = {'Frustrated', 'Gloat', 'Flinch'}
VARIANT_IDLE_SPEED = {1: 1.0, 2: 1.15, 3: 1.3}   # a boss on fewer lives fidgets faster


def guid(name):
    return hashlib.md5(('BossFightArms/' + name).encode()).hexdigest()


def num(v):
    return repr(round(float(v), 6)) if abs(v) > 1e-9 else '0'


def vec(x, y, z):
    return '{x: %s, y: %s, z: %s}' % (num(x), num(y), num(z))


def keys(times, values, loop):
    """Hermite keys with central-difference slopes (wrapping for loops)."""
    n = len(times)
    out = []
    for i in range(n):
        if loop:
            step = times[1] - times[0]
            prev, nxt = values[(i - 1) % (n - 1) if i else n - 2], values[(i + 1) % (n - 1) if i < n - 1 else 1]
            dt = 2.0 * step
        else:
            lo, hi = max(i - 1, 0), min(i + 1, n - 1)
            prev, nxt = values[lo], values[hi]
            dt = times[hi] - times[lo]
        slope = [(nxt[k] - prev[k]) / dt if dt else 0 for k in range(len(values[i]))]
        out.append((times[i], values[i], slope))
    return out


def vector_curve(path, ks):
    lines = ['  - curve:', '      serializedVersion: 2', '      m_Curve:']
    for t, v, s in ks:
        lines += ['      - serializedVersion: 3', f'        time: {num(t)}', f'        value: {vec(*v)}', f'        inSlope: {vec(*s)}', f'        outSlope: {vec(*s)}',
                  '        tangentMode: 0', '        weightedMode: 0', '        inWeight: {x: 0.33333334, y: 0.33333334, z: 0.33333334}',
                  '        outWeight: {x: 0.33333334, y: 0.33333334, z: 0.33333334}']
    lines += ['      m_PreInfinity: 2', '      m_PostInfinity: 2', '      m_RotationOrder: 4', f'    path: {path}']
    return '\n'.join(lines)


def float_curve(path, attribute, ks, index):
    lines = ['  - serializedVersion: 2', '    curve:', '      serializedVersion: 2', '      m_Curve:']
    for t, v, s in ks:
        lines += ['      - serializedVersion: 3', f'        time: {num(t)}', f'        value: {num(v[index])}', f'        inSlope: {num(s[index])}', f'        outSlope: {num(s[index])}',
                  '        tangentMode: 0', '        weightedMode: 0', '        inWeight: 0.33333334', '        outWeight: 0.33333334']
    lines += ['      m_PreInfinity: 2', '      m_PostInfinity: 2', '      m_RotationOrder: 4', f'    attribute: {attribute}', f'    path: {path}', '    classID: 4', '    script: {fileID: 0}', '    flags: 0']
    return '\n'.join(lines)


def clip_yaml(name, state, arms, head):
    fn, length, loops = poses.CLIPS[state]
    n = int(round(length * KEY_RATE[state]))
    times = [length * i / n for i in range(n + 1)]
    # Rest layout is the SCENE's effective one (prefab values plus the scene's overrides), so playing a clip never moves a
    # tentacle away from where the arena puts it. Rotation is about the *scaled* shoulder, added to the rest rotation.
    parts = {'Head': (head['rest'], head['rest_z'], (0.0, 0.0), head['rest_rot'])}
    for arm, a in arms.items():
        parts[arm] = (a['rest'], a['rest_z'], (a['shoulder'][0] * a['scale'][0], a['shoulder'][1] * a['scale'][1]), a['rest_rot'])
    eulers, positions, e_edit, p_edit = [], [], [], []
    for part, (rest, rz, sh, r0) in parts.items():
        rot_vals, pos_vals = [], []
        for t in times:
            rot, dx, dy = poses.sample(state, part, t) if not loops or t < length else poses.sample(state, part, 0.0)
            a0, a1 = math.radians(r0), math.radians(r0 + rot)
            rx = lambda a: (sh[0] * math.cos(a) - sh[1] * math.sin(a), sh[0] * math.sin(a) + sh[1] * math.cos(a))
            (x0, y0), (x1, y1) = rx(a0), rx(a1)
            rot_vals.append((0.0, 0.0, r0 + rot))
            pos_vals.append((rest[0] + x0 - x1 + dx, rest[1] + y0 - y1 + dy, rz))
        ek, pk = keys(times, rot_vals, loops), keys(times, pos_vals, loops)
        eulers.append(vector_curve(part, ek))
        positions.append(vector_curve(part, pk))
        e_edit.append(float_curve(part, 'localEulerAnglesRaw.z', ek, 2))
        for axis, label in enumerate('xyz'):
            p_edit.append(float_curve(part, f'm_LocalPosition.{label}', pk, axis))
    head_ = [
        '%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!74 &7400000', 'AnimationClip:', '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}',
        f'  m_Name: {name}', '  serializedVersion: 7', '  m_Legacy: 0', '  m_Compressed: 0', '  m_UseHighQualityCurve: 1',
        '  m_RotationCurves: []', '  m_CompressedRotationCurves: []', '  m_EulerCurves:']
    tail = [
        '  m_ScaleCurves: []', '  m_FloatCurves: []', '  m_PPtrCurves: []', f'  m_SampleRate: {FPS}', '  m_WrapMode: 0', '  m_Bounds:',
        '    m_Center: {x: 0, y: 0, z: 0}', '    m_Extent: {x: 0, y: 0, z: 0}', '  m_ClipBindingConstant:', '    genericBindings: []', '    pptrCurveMapping: []',
        '  m_AnimationClipSettings:', '    serializedVersion: 2', '    m_AdditiveReferencePoseClip: {fileID: 0}', '    m_AdditiveReferencePoseTime: 0',
        '    m_StartTime: 0', f'    m_StopTime: {num(length)}', '    m_OrientationOffsetY: 0', '    m_Level: 0', '    m_CycleOffset: 0',
        '    m_HasAdditiveReferencePose: 0', f'    m_LoopTime: {1 if loops else 0}', '    m_LoopBlend: 0', '    m_LoopBlendOrientation: 0',
        '    m_LoopBlendPositionY: 0', '    m_LoopBlendPositionXZ: 0', '    m_KeepOriginalOrientation: 0', '    m_KeepOriginalPositionY: 1',
        '    m_KeepOriginalPositionXZ: 0', '    m_HeightFromFeet: 0', '    m_Mirror: 0', '  m_EditorCurves:']
    body = '\n'.join(head_) + '\n' + '\n'.join(eulers) + '\n  m_PositionCurves:\n' + '\n'.join(positions) + '\n' + '\n'.join(tail) + '\n' \
        + '\n'.join(p_edit) + '\n  m_EulerEditorCurves:\n' + '\n'.join(e_edit) + '\n  m_HasGenericRootTransform: 0\n  m_HasMotionFloatCurves: 0\n  m_Events: []\n'
    return body


def meta(name, kind, main_id):
    importer = 'NativeFormatImporter'
    return (f'fileFormatVersion: 2\nguid: {guid(name)}\n{importer}:\n  externalObjects: {{}}\n  mainObjectFileID: {main_id}\n'
            '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


def controller_yaml(variant):
    cname = f'BossArms_v{variant}'
    sm, ctl = 1107000, 9100000
    states = {s: 1102000 + i for i, s in enumerate(STATE_ORDER)}
    trans = {s: 1101000 + i for i, s in enumerate(STATE_ORDER) if s in ONE_SHOT_RETURNS}
    out = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', f'--- !u!91 &{ctl}', 'AnimatorController:', '  m_ObjectHideFlags: 0',
           '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}',
           f'  m_Name: {cname}', '  serializedVersion: 5', '  m_AnimatorParameters:', '  - m_Name: Speed', '    m_Type: 1', '    m_DefaultFloat: 1',
           '    m_DefaultInt: 0', '    m_DefaultBool: 0', f'    m_Controller: {{fileID: {ctl}}}', '  m_AnimatorLayers:', '  - serializedVersion: 5',
           '    m_Name: Base Layer', f'    m_StateMachine: {{fileID: {sm}}}', '    m_Mask: {fileID: 0}', '    m_Motions: []', '    m_Behaviours: []',
           '    m_BlendingMode: 0', '    m_SyncedLayerIndex: -1', '    m_DefaultWeight: 0', '    m_IKPass: 0', '    m_SyncedLayerAffectsTiming: 0',
           f'    m_Controller: {{fileID: {ctl}}}']
    for i, state in enumerate(STATE_ORDER):
        clip = f'BossArms_v{variant}_{state}'
        speed = VARIANT_IDLE_SPEED[variant] if state == 'Idle' else 1
        trs = f'\n  - {{fileID: {trans[state]}}}' if state in trans else ' []'
        out += [f'--- !u!1102 &{states[state]}', 'AnimatorState:', '  serializedVersion: 6', '  m_ObjectHideFlags: 1', '  m_CorrespondingSourceObject: {fileID: 0}',
                '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', f'  m_Name: {state}', f'  m_Speed: {speed}', '  m_CycleOffset: 0',
                f'  m_Transitions:{trs}', '  m_StateMachineBehaviours: []', f'  m_Position: {{x: {220 + 30 * (i % 2)}, y: {40 + 60 * i}, z: 0}}',
                '  m_IKOnFeet: 0', '  m_WriteDefaultValues: 1', '  m_Mirror: 0', '  m_SpeedParameterActive: 1', '  m_MirrorParameterActive: 0',
                '  m_CycleOffsetParameterActive: 0', '  m_TimeParameterActive: 0', f'  m_Motion: {{fileID: 7400000, guid: {guid(clip)}, type: 2}}',
                '  m_Tag: ', '  m_SpeedParameter: Speed', '  m_MirrorParameter: ', '  m_CycleOffsetParameter: ', '  m_TimeParameter: ']
    for state, tid in trans.items():
        out += [f'--- !u!1101 &{tid}', 'AnimatorStateTransition:', '  m_ObjectHideFlags: 1', '  m_CorrespondingSourceObject: {fileID: 0}',
                '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', '  m_Name: ', '  m_Conditions: []', '  m_DstStateMachine: {fileID: 0}',
                f'  m_DstState: {{fileID: {states["Idle"]}}}', '  m_Solo: 0', '  m_Mute: 0', '  m_IsExit: 0', '  serializedVersion: 3', '  m_TransitionDuration: 0.12',
                '  m_TransitionOffset: 0', '  m_ExitTime: 1', '  m_HasExitTime: 1', '  m_HasFixedDuration: 1', '  m_InterruptionSource: 0',
                '  m_OrderedInterruption: 1', '  m_CanTransitionToSelf: 1']
    out += [f'--- !u!1107 &{sm}', 'AnimatorStateMachine:', '  serializedVersion: 6', '  m_ObjectHideFlags: 1', '  m_CorrespondingSourceObject: {fileID: 0}',
            '  m_PrefabInstance: {fileID: 0}', '  m_PrefabAsset: {fileID: 0}', '  m_Name: Base Layer', '  m_ChildStates:']
    for i, state in enumerate(STATE_ORDER):
        out += ['  - serializedVersion: 1', f'    m_State: {{fileID: {states[state]}}}', f'    m_Position: {{x: {220 + 30 * (i % 2)}, y: {40 + 60 * i}, z: 0}}']
    out += ['  m_ChildStateMachines: []', '  m_AnyStateTransitions: []', '  m_EntryTransitions: []', '  m_StateMachineTransitions: {}',
            '  m_StateMachineBehaviours: []', '  m_AnyStatePosition: {x: 50, y: 20, z: 0}', '  m_EntryPosition: {x: 50, y: 120, z: 0}',
            '  m_ExitPosition: {x: 800, y: 120, z: 0}', '  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}', f'  m_DefaultState: {{fileID: {states["Idle"]}}}']
    return '\n'.join(out) + '\n'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for folder in (OUT, OUT.parent):
        meta_path = folder.parent / (folder.name + '.meta')
        if not meta_path.exists():
            meta_path.write_text(f'fileFormatVersion: 2\nguid: {guid("folder/" + folder.name)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    count = 0
    for variant in (1, 2, 3):
        arms, head, _ = rig_data.load(variant)
        for state in STATE_ORDER:
            name = f'BossArms_v{variant}_{state}'
            (OUT / f'{name}.anim').write_text(clip_yaml(name, state, arms, head))
            (OUT / f'{name}.anim.meta').write_text(meta(name, 'anim', 7400000))
            count += 1
        cname = f'BossArms_v{variant}'
        (OUT / f'{cname}.controller').write_text(controller_yaml(variant))
        (OUT / f'{cname}.controller.meta').write_text(meta(cname, 'controller', 9100000))
    print('wrote', count, 'clips and 3 controllers to', OUT.relative_to(ROOT))


if __name__ == '__main__':
    main()
