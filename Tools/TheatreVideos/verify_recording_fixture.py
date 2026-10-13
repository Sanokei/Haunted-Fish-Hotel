#!/usr/bin/env python3
"""Authored guide/geometry checks. No Unity import, physics or capture is executed."""
import json
import math
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/ScenePresentationTests'))
import verify_scene_assets as assets
from capture_contract import instructions

checks = 0

def check(value, message):
    global checks
    checks += 1
    assert value, message


def text(path): return (ROOT / path).read_text()
def docs(path): return {record.ident: record for record in assets.records(text(path))}
def guid(path): return re.search(r'^guid: (\w+)', text(path + '.meta'), re.M)[1]


def main():
    stage_path = 'Assets/Resources/Tutorials/TutorialRecordingStage.prefab'
    stage = docs(stage_path)
    director = stage[242606801975031782].body
    for field in ('Fish', 'OtherFish', 'Ghost', 'Haunting', 'FishStunEffect', 'FishStunAnimation', 'World'):
        check(assets.local_ref(director, field) in stage, 'Authored director reference resolves: ' + field)
    check(assets.local_ref(director, 'Fish') != assets.local_ref(director, 'OtherFish'), 'Angler uses two distinct authored fish')
    player_guid = guid('Assets/Resources/HotelNetworkPlayer.prefab')
    actors = [record for record in stage.values() if record.kind == 1001 and 'guid: ' + player_guid in record.body]
    check(len(actors) == 3, 'Stage has exactly three linked production actor instances')
    for actor in actors:
        check('propertyPath: Networked\n      value: 0' in actor.body, 'Every recording actor is authored offline')
        check('propertyPath: m_Enabled\n      value: 0' not in actor.body, 'Recording actor component is enabled for production status/physics')
        check('_RunningJumpSpeed' not in actor.body and 'JumpSpeed' not in actor.body and 'Gravity' not in actor.body,
              'Recording actor inherits production jump tuning without overrides')
    for field, source_id in [('FishStunEffect', 883400003), ('FishStunAnimation', 883400004)]:
        record = stage[assets.local_ref(director, field)]
        check(record.stripped and f'fileID: {source_id}, guid: {player_guid}' in record.body,
              'Piranha observation binds the actual nested production effect: ' + field)
    check('m_SourcePrefab: {fileID: 100100000, guid: ' + guid(stage_path) in text('Assets/Scenes/TutorialRecording.unity'),
          'Existing recording scene inherits the updated authored stage')

    for source, target, field in [('ShoppingCartTrap', 'TutorialCart', 'CartDefinition'), ('FallingChandelierTrap', 'TutorialChandelier', 'ChandelierDefinition'), ('ClickTrap', 'TutorialClickTrap', 'ClickDefinition'), ('AnglerFishTrap', 'TutorialAngler', 'AnglerDefinition')]:
        production = text(f'Assets/Resources/Traps/{source}.prefab')
        tutorial_path = f'Assets/Resources/Tutorials/{target}.prefab'
        tutorial = text(tutorial_path)
        if source == 'FallingChandelierTrap':
            normalize = lambda value: re.sub(r'^  _(ArmedOffset|FallDistance):.*$', r'  _\1: recording-height', value, flags=re.M)
            production, tutorial = normalize(production), normalize(tutorial)
        check(production == tutorial, target + ' retains current production art, rules, danger band and split/light bindings')
        check('guid: ' + guid(tutorial_path) in assets.value(director, field), target + ' is bound to the recording director')
        check(not assets.syntax_issues(text(tutorial_path)), target + ' YAML parses')
        check(not assets.reference_issues(text(tutorial_path)), target + ' local object references resolve')
    check(not assets.syntax_issues(text(stage_path)), 'Stage YAML parses')
    check(not assets.reference_issues(text(stage_path)), 'Stage local object references resolve')
    source = text('Assets/Scripts/Tutorials/TutorialRecordingDirector.cs')
    supported = set(re.findall(r'case "([^"]+)":', source))
    shot_names = set(re.findall(r'^  - Name: ([^\n]+)', director, re.M))
    plan = json.loads(text('Tools/TheatreVideos/recording-plan.json'))
    for role in ('Fish', 'Ghost'):
        operations = instructions(ROOT / f'Assets/Dialogue/TutorialRecording/{role}Recording.ink')
        for kind, values in operations:
            if kind in ('TutorialDemo', 'TutorialSetup'):
                check(values[0] in supported, role + ' has supported actual action: ' + values[0])
            if kind == 'TutorialShot': check(values[0] in shot_names, role + ' has authored camera shot: ' + values[0])
        headings = [values[0] for kind, values in operations if kind == 'TutorialCaption']
        check(set(plan['required_headings_before_publish'][role]).issubset(headings), role + ' contains every required intertitle')
        for observation in plan['required_capture_checks'][role]:
            check('"' + observation + '"' in source, role + ' check is emitted only by actual director assertions: ' + observation)
    check('Movement.Simulate(' not in source, 'Director never double-ticks the production actor motor')
    check('Guid.NewGuid()' in source, 'Each capture run has an isolated round key')
    check('GetComponent<GameSideScrollMotor>().Unbind(this)' in source and 'actor.enabled = false' in source,
          'Recording teardown releases the input environment and actors')
    check('catch (Exception error) { Fail(error); throw; }' in source, 'Begin failure releases owned bindings')
    check(not re.search(r'\b(?:Instantiate|AddComponent)\s*\(|new\s+GameObject\s*\(', source), 'No runtime guide object/UI construction')
    check(not re.search(r'\.(?:Health|IsDead)\s*=(?!=)|DeathEmergence\.PlayAt|FishStunAnimation\.Play\(', source),
          'Recording never fabricates health/death/effect results')
    batch = text('Assets/Editor/Tutorials/TutorialRecordingBatch.cs')
    check('ExportCaptureManifest();' in batch and 'File.Copy(source, target' not in batch,
          'Native capture exports observations without overwriting shipped guide clips')
    print(f'PASS: {checks} recording source/serialized-fixture checks. Native import, rendering, physics and capture remain unverified.')


if __name__ == '__main__': main()
