#!/usr/bin/env python3
"""Source/serialization checks only. Does not import or run Unity."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/ScenePresentationTests'))
import verify_scene_assets as assets

checks = 0

def check(value, message):
    global checks
    checks += 1
    assert value, message

def text(path): return (ROOT / path).read_text()
def docs(path): return {record.ident: record for record in assets.records(text(path))}

def main():
    driver = text('Assets/Scripts/Multiplayer/AuthoredGhostEmergence.cs')
    haunting = text('Assets/Scripts/Multiplayer/GameHauntingController.cs')
    player_source = text('Assets/Scripts/Multiplayer/HotelPlayer.cs')
    for source, label in ((driver, 'authored effect'), (haunting, 'death observer')):
        check(not re.search(r'\b(?:Instantiate|Destroy|AddComponent)\s*\(|new\s+GameObject\s*\(', source), label + ' has no runtime object creation or destruction')
    check('StorySequenceStepKind.CreateUI' in driver and 'throw new InvalidOperationException' in driver, 'effect rejects story-driven runtime UI creation')
    check('_DeathPosition = position;' in driver and 'WorldToScreenPoint(_DeathPosition)' in driver, 'animation uses the replicated event position')
    check('transform.position' not in driver, 'effect does not follow later actor motion')
    check('gameObject.SetActive(false)' in driver and 'void OnDisable()' in driver, 'effect hides after completion and cancels on disable')
    check('ScreenPointToWorldPointInRectangle' in driver and '_Canvas.worldCamera = camera' in driver, 'nested canvas projects onto the explicitly bound camera plane')
    check('camera.nearClipPlane + .01f' in driver and '_Canvas.scaleFactor' in driver, 'canvas projection accounts for clipping and UI scale')
    check('TryContext(out _, out var floorY)' in haunting and 'fish.BecomeSpectator(floorY)' in haunting and 'floorY = scene.HallwayBounds.max.y' in haunting, 'death retains the scoped floor corpse/spectator path and production hallway default')
    check('fish.GhostEmergencePosition = fish.transform.position;' in haunting and 'fish.GhostEmergenceVersion++;' in haunting, 'death publishes the retained corpse position and one new event version')
    check('GhostId =' not in haunting, 'death never assigns a new ghost role')
    check('player.GhostEmergenceVersion > seen && player.IsDead' in haunting, 'only a new death version plays the effect')
    check('_DeathEmergence.Stop()' in player_source, 'player lifecycle releases its authored effect')

    effect_path = 'Assets/Prefabs/Effects/DeathEmergence.prefab'
    effect = docs(effect_path)
    guid = re.search(r'^guid: (\w+)', text('Assets/Scripts/Multiplayer/AuthoredGhostEmergence.cs.meta'), re.M)[1]
    check(assets.value(effect[500001].body, 'm_IsActive') == '0', 'effect root is initially disabled')
    check(assets.value(effect[500003].body, 'm_RenderMode') == '1', 'nested effect uses Screen Space Camera rather than a root-only overlay canvas')
    check(assets.value(effect[500003].body, 'm_Enabled') == '0', 'canvas stays hidden until the driver binds a camera')
    check(assets.local_ref(effect[500006].body, '_Canvas') == 500003, 'driver references the authored camera-space canvas')
    check('guid: ' + guid in effect[500006].body, 'prefab binds the reusable effect driver')
    check('  _DestroyOnComplete:' not in text(effect_path), 'one-shot destruction setting is removed')
    check('  _Prefabs: []' in effect[500005].body, 'death story has no object-creation templates')
    check('  _ListenToStoryEvents: 0' in effect[500005].body, 'death story cannot receive unrelated global story commands')
    for name in ('soul_appear', 'soul_rise', 'soul_clear'):
        check('Tag: ' + name in effect[500005].body, 'original authored layout preserved: ' + name)
    check('Position: {x: 0, y: 150}' in effect[500005].body, 'ghost still rises above the corpse')

    player_path = 'Assets/Resources/HotelNetworkPlayer.prefab'
    player = docs(player_path)
    ref = assets.local_ref(player[21000005].body, '_DeathEmergence')
    check(player[ref].stripped and 'fileID: 500006' in player[ref].body, 'player resolves its nested death-effect component')
    check('m_TransformParent: {fileID: 21000002}' in player[883500001].body, 'death effect is authored under the actor root')
    check('{fileID: 883500002}' in player[21000002].body, 'actor root includes the authored effect child')
    check(883400001 in player and 883400003 in player and 883400004 in player, 'piranha nested prefab records are preserved')
    check('_EmergencePrefab:' not in text('Assets/Scenes/Game.unity'), 'scene no longer binds a runtime-spawn template')
    for path in (effect_path, player_path):
        check(not assets.syntax_issues(text(path)), path + ': YAML syntax is valid')
        check(not assets.reference_issues(text(path)), path + ': local references resolve')
    print(f'PASS: {checks} authored death-emergence source/serialization checks (not Unity import or playback).')

if __name__ == '__main__': main()
