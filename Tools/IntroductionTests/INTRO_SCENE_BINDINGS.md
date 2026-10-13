# Urgent lobby intro binding repair

The validation failure is expected when the required world presentation is unbound.
Validation now distinguishes an absent _World from the exact unsupported shot/caption.
Do not remove validation or silently omit world steps.

Existing LobbyIntroductionPresentation (script GUID c4b0bc196f204d9e9ce66a9e0019b8ee):
- Keep existing _UIPrefab.
- Assign _World to a new LobbyIntroductionWorldSequence component.

LobbyIntroductionWorldSequence script:
Assets/Scripts/Multiplayer/Lobby/LobbyIntroductionWorldSequence.cs
GUID a19cc654b8ee4ab0a01c4f444a6c1a16
Required serialized fields and types:
- _Camera: Unity.Cinemachine.CinemachineCamera. Use existing LobbyCam component 1933697368.
- _EntryCamera: UnityEngine.Transform on a separate immutable authored entry-camera pose marker.
- _FishStart: UnityEngine.Transform on a foreground fish-position marker in front of entry camera.
- _FishWalkEnd: UnityEngine.Transform farther into the lobby, revealing interior during the walk.
- _ReceptionCamera: UnityEngine.Transform on a separate immutable reception-camera pose marker;
  the path from entry to reception should pass the fish toward the desk.
- _Caption: TMPro.TMP_Text on an independently authored, active Canvas. A screen-overlay text
  near the lower center is safe; world text requires an authored Canvas facing the output camera.
  Do not borrow the live DialogueManager panel's SpeechText if its parent panel/canvas is inactive.

Do not bind _EntryCamera or _ReceptionCamera to the moving LobbyCam.transform itself.
They must retain authored pose values while the actual camera animates.
No explicit player/fish binding is required: Play waits up to 15 seconds for LocalPlayer,
clones its authored HotelFishSprite for client-only presentation, hides original sprite
renderers temporarily, and never teleports the network body.

Completion/skip/disable Close restores original renderer visibility, camera pose/follow/look-at
and the spline dolly enabled state, hides the caption and destroys the temporary fish visual.
The existing LobbyManager restores controls. Caption text is exactly:
Looks like nobody has checked into this place for years

Setup validation regression now passes missing-world, missing-caption, missing-reception and
complete-binding cases through the production helper. Native scene binding remains owner work.

Latest regressions: 60 intro/door/setup checks passed, 55 storm checks passed.
Door clarification: only the Door section background shape (fileID 3720855840725677550) and its rest-layout Slant now start at 0. The existing push still targets Slant 120. Knocking door child sprite/hinge/rotation/pivot/timing are unchanged. StoryUI restores authored poses on close/replay.
LobbyManager now marks the intro played only after validation and UI preparation succeed, allowing a repaired configuration to retry via PrepareIntroduction().
