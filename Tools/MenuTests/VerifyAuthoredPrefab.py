from pathlib import Path
import re
repo=Path(__file__).resolve().parents[2]
import sys
native_repo=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else repo
path=repo/'Assets/Resources/MemberMenu.prefab'
source=path.read_text(encoding='utf-8-sig')
headers=list(re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',source,re.M|re.S))
records={int(m.group(2)):(int(m.group(1)),m.group(3)) for m in headers}
checks=0
def check(value,why):
 global checks
 assert value,why
 checks+=1
def ref(text,name):
 match=re.search(r'^  '+re.escape(name)+r': \{fileID: (\d+)',text,re.M)
 check(bool(match),'missing authored binding '+name)
 return int(match.group(1))
check(len(records)==len(headers),'duplicate YAML file IDs')
for line in source.splitlines():
 if 'guid:' in line: continue
 for ident in re.findall(r'fileID: (\d+)',line):
  check(int(ident)==0 or int(ident) in records,'unresolved local reference '+line)
owner=records[920000001][1]
pres_id=ref(owner,'_Presentation'); pres=records[pres_id][1]
check(records[pres_id][0]==114,'presentation must be an authored MonoBehaviour')
for name in ['_Root','_Roster','_Settings','_Canvas','_Blocker','_ModalGroup','_Resume','_RoomTab','_SettingsTab','_Apply','_Cancel','_DisplayChange','_Volume','_VolumeLabel','_DisplayLabel','_Status','_Heading','_HowTo','_Tutorial','_HowToTab','_MusicVolume','_EffectsVolume','_MusicVolumeLabel','_EffectsVolumeLabel']:
 ident=ref(pres,name); check(ident in records,'missing serialized '+name)
root=ref(owner,'_Menu'); group=ref(pres,'_ModalGroup')
check(records[group][0]==225,'missing CanvasGroup regression')
check(ref(records[group][1],'m_GameObject')==root,'CanvasGroup attached to wrong object')
check('m_Alpha: 1' in records[group][1] and 'm_BlocksRaycasts: 1' in records[group][1],'modal visibility/capture')
check('m_IsActive: 0' in records[root][1],'menu must start closed')
check('m_RenderMode: 0' in records[28000003][1] and 'm_OverrideSorting: 1' in records[28000003][1] and 'm_SortingOrder: 32767' in records[28000003][1],'overlay sorting')
slider=ref(pres,'_Volume'); check('67db9e8f0e2ae9c40bc1e2b64352a6b4' in records[slider][1],'authored Slider script')
check(ref(records[slider][1],'m_FillRect') in records and ref(records[slider][1],'m_HandleRect') in records,'authored slider geometry')
for name in ['MemberMenu','MemberMenuPresentation','MemberMenuTutorial']:
 code=((native_repo if name=='MemberMenu' else repo)/f'Assets/Scripts/Multiplayer/{name}.cs').read_text()
 check(not re.search(r'new\s+GameObject|\.AddComponent\s*[<(]|new\s+MemberMenuPresentation|CreateFontAsset|\.SetParent\(',code),name+' must not build UI at runtime')
check('new GameObject' not in (native_repo/'Assets/Editor/MemberMenuValidation.cs').read_text(),'validator must reuse authored prefab')
check('SceneManager.SetActiveScene' not in (native_repo/'Assets/Editor/MemberMenuValidation.cs').read_text(),'preview-scene failure regression')
fallback=(native_repo/'Assets/Resources/MemberMenuEventSystem.prefab').read_text()
check('76c392e42b5098c458856cdf6ecaaaa1' in fallback and '01614664b831546d2ae94a42149d80ac' in fallback,'authored EventSystem and InputSystem module')
check('_EventSystemPrefab:' in owner,'fallback prefab binding')
fallback_records={int(m.group(2)):(int(m.group(1)),m.group(3)) for m in re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',fallback,re.M|re.S)}
for line in fallback.splitlines():
 if 'guid:' in line: continue
 for ident in re.findall(r'fileID: (\d+)',line):
  check(int(ident)==0 or int(ident) in fallback_records,'unresolved fallback reference '+line)

howto=ref(pres,'_Tutorial')
for name in ['_Fish','_Ghost','_Playback','_Replay','_Title','_Instructions','_Status','_PlaybackLabel','_Scroll','_Surface','_Video','_Audio']:
 ident=ref(records[howto][1],name);check(ident in records,'missing tutorial '+name)
check('m_AudioOutputMode: 1' in records[ref(records[howto][1],'_Video')][1],'tutorial video must route through authored source')
check('24300002' in records[ref(records[howto][1],'_Audio')][1],'tutorial audio must use effects bus')
check('RoundHowToPage' not in (repo/'Assets/Scripts/Multiplayer/MemberMenuTutorial.cs').read_text(),'tutorial browsing must not use ready-up gating')
for name in ['_MusicVolume','_EffectsVolume']:
 slider=ref(pres,name);check('67db9e8f0e2ae9c40bc1e2b64352a6b4' in records[slider][1],'category control must be authored Slider')
 check(ref(records[slider][1],'m_FillRect') in records and ref(records[slider][1],'m_HandleRect') in records,'category slider geometry')

tutorial=(repo/'Assets/Scripts/Multiplayer/MemberMenuTutorial.cs').read_text()
check('HotelTutorialLibrary' not in tutorial,'consumer must use recorder content without a duplicate schema')
check('TutorialRoleContent _FishContent, _GhostContent' in tutorial,'typed recorder content bindings')
for field,guid,resource in [('_FishContent','93c30050cfaa4925ad411db26f86ab56','Tutorials/FishTutorialContent'),('_GhostContent','6621df816dd3462bb20cab789286533c','Tutorials/GhostTutorialContent')]:
 check(field+': {fileID: 11400000, guid: '+guid in records[howto][1],'authored recorder asset '+field)
 check('Resources.Load<TutorialRoleContent>("'+resource+'")' in tutorial,'recorder resource fallback '+field)
check('foreach (var section in content.Instructions)' in tutorial,'both roles must render all instruction sections')
check('content.Recording' in tutorial and 'content.Loop' in tutorial and 'content.HasAudio' in tutorial,'recording/loop/audio contract')
check('VideoAudioOutputMode.None' in tutorial and '_Audio.mute = !hasAudio' in tutorial,'silent recording must be muted')
check('_Surface.gameObject.SetActive' not in tutorial,'hiding video image must not disable the co-located player')
surface=records[ref(records[howto][1],'_Surface')][1]
check('    width: 1\n    height: 1' in surface,'video UV rectangle must cover the full texture')
check(not (repo/'Assets/Scripts/Multiplayer/HotelTutorialLibrary.cs').exists(),'duplicate content schema removed')

preview=ref(records[howto][1],'_GhostPreview')
check('ad74e6bf18f9482baf05852f6c78e8ab' in records[preview][1],'controlled Ghost thumbnail must use BOSS-cursor artwork')
check('m_PreserveAspect: 1' in records[preview][1] and 'm_RaycastTarget: 0' in records[preview][1],'thumbnail keeps artwork proportions and does not capture clicks')
check('_GhostPreview.enabled = ghost' in tutorial and '_GhostPreview.enabled = false' in tutorial,'authored ghost thumbnail follows playback preparation')
heading_guid='e08468cb765749c59b1362447214b0e5'
check(heading_guid in records[950000008][1] and heading_guid in records[980000200][1],'scoped Modak display headings')
check('98067b61ac5446dba12771373453ce8a' in records[980000358][1],'instructions use rounded readable Varela body')
check('m_fontSize: 24' in records[980000358][1] and 'm_lineSpacing: 2' in records[980000358][1] and 'm_paragraphSpacing: 4' in records[980000358][1],'instruction size and leading')
check('m_SizeDelta: {x: 390, y: 48}' in records[980000198][1],'role title box respects larger Modak line height')
check('m_SizeDelta: {x: 380, y: 230}' in records[980000224][1],'instruction viewport clears title')
font=(repo/'Assets/Resources/HalloweenUI/Modak-Headings.asset').read_text()
check('m_SourceFontFileGUID: d4e80c7736f5472abd2cace956a4fda6' in font and 'm_FamilyName: Modak' in font,'licensed bundled Modak source')
check('m_FamilyName: Modak' in font and 'Creepster' not in font,'heading keeps correct source family after native bake')
check('HotelTypographyAuthoring.BakeHeadingFont()' in (repo/'Assets/Editor/MemberMenuValidation.cs').read_text(),'existing native font helper cannot restore prior heading font')

body=(repo/'Assets/Resources/HalloweenUI/VarelaRound-Body.asset').read_text()
check('m_FamilyName: Varela Round' in body and 'm_SourceFontFileGUID: da9a51ce28f0d7443a5bcbdbe04c4f4b' in body,'body uses bundled licensed Varela source')
check('m_AtlasPopulationMode: 1' in body and 'm_FamilyName: Varela Round' in body,'native body atlas keeps correct dynamic source family')
check('BakeBodyFontOnly' in (repo/'Assets/Editor/HotelTypographyAuthoring.cs').read_text(),'recorder can bake body without changing approved heading')

check('m_TextWrappingMode: 1' in records[980000358][1], 'instructions use installed TMP Normal wrapping, not NoWrap')

print(f'PASS: {checks} authored-prefab/reference/ownership assertions. This validates serialized source, not runtime rendering or connected input.')

