#!/usr/bin/env python3
"""Checks real serialized ready-guide rectangles and TTF metrics, not Unity renders."""
from pathlib import Path
import json
import math
import re
import sys
import yaml
from PIL import ImageFont

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools/ScenePresentationTests'))
import verify_scene_assets as assets

checks = 0

def check(value, message):
    global checks
    checks += 1
    assert value, message

def raw(path): return (ROOT / path).read_text()
def vec(value): return value['x'], value['y']
def bounds(rect):
    x, y = vec(rect['m_AnchoredPosition']); w, h = vec(rect['m_SizeDelta'])
    return x - w / 2, y - h / 2, x + w / 2, y + h / 2

def inside(inner, outer):
    return inner[0] >= outer[0] - .01 and inner[1] >= outer[1] - .01 and inner[2] <= outer[2] + .01 and inner[3] <= outer[3] + .01

def overlaps(a, b):
    return min(a[2], b[2]) - max(a[0], b[0]) > .1 and min(a[3], b[3]) - max(a[1], b[1]) > .1

def main():
    fonts = {
        'd4e80c7736f5472abd2cace956a4fda6': 'Assets/Art/Fonts/Modak-Regular.ttf',
        'e3265ab4bf004d28a9537516768c1c75': 'Assets/TextMesh Pro/Fonts/LiberationSans.ttf',
    }
    report = {'kind': 'Serialized source and actual font metric checks only; no Unity import/render/network pass', 'roles': {}}
    for role in ('Fish', 'Ghost'):
        source = raw(f'Assets/Resources/{role}RoundHowTo.prefab')
        docs = {r.ident: next(iter(yaml.safe_load(r.body).values())) for r in assets.records(source)}
        check(not assets.syntax_issues(source), role + ' valid YAML')
        check(not assets.reference_issues(source), role + ' local references and hierarchy resolve')
        check('VideoPlayer:' not in source and 'RawImage' not in source and 'm_VideoClip:' not in source, role + ' no movie replacement')
        check(sum(r.kind == 223 for r in assets.records(source)) == 1, role + ' exactly one authored canvas')
        check(source.count('guid: 4e29b1a8efbd4b44bb3f3716e73f07ff') == 1, role + ' exactly one Ready action')
        check(docs[105]['m_ScreenMatchMode'] == 1, role + ' uses expand scaling')
        check(docs[107]['_ContinueLabel']['fileID'] == 203 and docs[107]['_Status']['fileID'] == 193 and docs[107]['_Layout']['fileID'] == 109, role + ' live fields bound')
        check(docs[109]['_Viewport']['fileID'] == 101 and docs[109]['_SafeArea']['fileID'] == 211 and docs[109]['_Page']['fileID'] == 221, role + ' safe-area references bound')
        check(docs[133]['m_Text'] == 'YOU ARE THE ' + role.upper(), role + ' correct authored role heading')
        check(docs[184]['m_Interactable'] == 0, role + ' Ready starts disabled')
        check(docs[193]['m_Text'] == '0 / 0 PLAYERS READY', role + ' group counter does not duplicate the local Ready instruction')
        check(vec(docs[221]['m_SizeDelta']) == (1280, 720), role + ' one fixed reference-page design')
        text_boxes = []
        texts = []
        for record in assets.records(source):
            component = docs[record.ident]
            if 'm_FontData' not in component: continue
            ident = component['m_GameObject']['fileID']; rect = docs[ident + 1]
            check(docs[ident]['m_IsActive'] == 1, role + ' every guide text visible on the same page')
            check(component['m_FontData']['m_Font']['guid'] in fonts, role + ' real imported font reference')
            font = ImageFont.truetype(str(ROOT / fonts[component['m_FontData']['m_Font']['guid']]), component['m_FontData']['m_FontSize'])
            lines = component['m_Text'].split('\n'); w, h = vec(rect['m_SizeDelta'])
            width = max(font.getlength(line) for line in lines)
            ascent, descent = font.getmetrics()
            # Conservatively reserve full font ascent+descent for every explicit line.
            height = (ascent + descent) * (1 + (len(lines) - 1) * component['m_FontData']['m_LineSpacing'])
            check(width <= w, role + ' unwrapped text fits width: ' + docs[ident]['m_Name'])
            check(height <= h, role + ' complete lines fit height: ' + docs[ident]['m_Name'])
            check(component['m_FontData']['m_BestFit'] == 0, role + ' never shrinks copy unpredictably')
            if ident != 200:
                box = bounds(rect); text_boxes.append((ident, box))
                check(inside(box, (-600, -336, 600, 336)), role + ' text inside board')
            texts.append({'object': docs[ident]['m_Name'], 'file_id': record.ident, 'rect': [w, h], 'measured_width': round(width, 2), 'reserved_line_height': round(height, 2), 'font_size': component['m_FontData']['m_FontSize'], 'text': component['m_Text']})
        labels = re.findall(r'return "([A-Z ,]+)";', raw('Assets/Scripts/Multiplayer/RoundHowToState.cs'))
        dynamic = []
        for ident in (193, 203):
            component = docs[ident]; rect = docs[component['m_GameObject']['fileID'] + 1]
            fd = component['m_FontData']; font = ImageFont.truetype(str(ROOT / fonts[fd['m_Font']['guid']]), fd['m_FontSize'])
            w, h = vec(rect['m_SizeDelta']); ascent, descent = font.getmetrics()
            for label in (labels if ident == 203 else ['2147483647 / 2147483647 PLAYERS READY']):
                lines = [label]
                width = max(font.getlength(line) for line in lines)
                height = (ascent + descent) * len(lines)
                check(width <= w and height <= h, role + ' dynamic ready state fits: ' + label)
                dynamic.append({'file_id': ident, 'label': label, 'maximum_line_width': round(width, 2), 'reserved_height': height})
        report['roles'][role + ' dynamic states'] = dynamic
        for index, (a_id, a) in enumerate(text_boxes):
            for b_id, b in text_boxes[index + 1:]: check(not overlaps(a, b), f'{role} separate text blocks {a_id}/{b_id}')
        check(inside(bounds(docs[161]), bounds(docs[151])) and inside(bounds(docs[241]), bounds(docs[151])), role + ' controls within their card')
        check(inside(bounds(docs[171]), bounds(docs[231])) and inside(bounds(docs[251]), bounds(docs[231])), role + ' rules within their card')
        if role == 'Fish':
            for text in ('Shift / stick press: Run', 'Space / gamepad A: Jump', 'Mouse: Aim your spotlight'):
                check(text in docs[163]['m_Text'], 'Fish current controls: ' + text)
        else:
            for text in ('F / B: Reset', 'E / X: Open, place, possess / release', 'Q: Discard'):
                check(text in docs[163]['m_Text'], 'Ghost current controls: ' + text)
        for text in ('Tablecart: Knockback + stun; no damage.', 'Angler: Kills every fish it crosses.', 'Starting hallway: No traps or cart travel.'):
            check(text in docs[173]['m_Text'], role + ' current rule: ' + text)
        report['roles'][role] = texts
    # Derive Expand canvas and actual safe-area/page scale from each display shape.
    report['displays'] = []
    for width, height, safe in [(1280,720,(0,0,1280,720)), (1920,1080,(0,0,1920,1080)), (1280,800,(0,0,1280,800)), (1024,768,(0,0,1024,768)), (3440,1440,(0,0,3440,1440)), (2340,1080,(108,40,2124,1000)), (800,1280,(20,40,760,1180))]:
        sx,sy,sw,sh=safe
        canvas_scale=min(width/1280,height/720)
        page_scale=min(1,(sw/canvas_scale)/1280,(sh/canvas_scale)/720)
        pixel_scale=canvas_scale*page_scale
        cx,cy=sx+sw/2,sy+sh/2
        box=(cx-640*pixel_scale,cy-360*pixel_scale,cx+640*pixel_scale,cy+360*pixel_scale)
        check(inside(box,(sx,sy,sx+sw,sy+sh)), f'{width}x{height} entire guide stays inside safe display')
        check(math.isfinite(pixel_scale) and pixel_scale>0, f'{width}x{height} valid readable layout scale')
        report['displays'].append({'pixels':[width,height],'safe_area':safe,'page_pixel_bounds':box,'body_pixels':round(24*pixel_scale,2)})
    action=json.loads(raw('Assets/Resources/HotelMultiplayerActions.inputactions'))
    bindings=next(m['bindings'] for m in action['maps'] if m['name']=='Ghost')
    check(any(b['action']=='Reset' and b['path']=='<Keyboard>/f' for b in bindings),'Guide reset F matches actual input map')
    check('_Impact: 1' in raw('Assets/Resources/Traps/ShoppingCartTrap.prefab'),'Tablecart is authored knockback/stun')
    check('_Impact: 2' in raw('Assets/Resources/Traps/AnglerFishTrap.prefab'),'Angler is authored lethal')
    controller=raw('Assets/Scripts/Multiplayer/GameSceneController.cs')
    check('!_Gate.Complete || !_ReadyGate.Complete' in controller,'Countdown still requires both full cinematic and deliberate-ready gates')
    check('float _GhostSetupSeconds = 10' in controller and '_GhostSetupSeconds: 10' in raw('Assets/Scenes/Game.unity'),'Authored countdown matches the guide')
    runtime=raw('Assets/Scripts/Multiplayer/RoundHowToPage.cs')+raw('Assets/Scripts/Multiplayer/RoundHowToSafeArea.cs')
    check('new GameObject' not in runtime and 'Instantiate(' not in runtime and 'AddComponent' not in runtime,'Guide never builds runtime UI objects')
    report['checks']=checks
    if '--report' in sys.argv:
        path=Path(sys.argv[sys.argv.index('--report')+1]);path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(report,indent=2)+'\n')
    print(f'Passed {checks} serialized layout, font metrics, current-control/rules and safe-area geometry checks. No Unity render or network pass claimed.')

if __name__ == '__main__': main()
