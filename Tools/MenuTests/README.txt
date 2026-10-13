Pause/settings verification
Run behavior tests: dotnet run --project Tools/MenuTests/MenuTests.csproj
Run authored asset integration checks: python Tools/MenuTests/VerifyAuthoredPrefab.py
The behavior suite compiles production settings and interaction-state source directly.
It covers load/apply, invalid values, Cancel/reopen, no editing effects, transition
admission, Escape priority during intro, held-closing-click suppression, missing
pointer devices, reopens and disable cleanup. Unity preference/audio effects are simulated.
The authored asset checks cover component/reference completeness, the CanvasGroup
regression, modal sorting, slider geometry, authored EventSystem and no runtime UI builders.
Actual prefab validation: Assets/Editor/MemberMenuValidation.cs auto-runs once in idle
Edit mode after successful import/compile, or via Tools > Haunted Hotel > Validate
authored pause menu and settings. It loads existing prefab contents, invokes production
Awake and checks Apply/Cancel/reopen/Back without constructing UI. It never activates
a preview scene, opens/saves authored scenes, or changes Play mode. It restores prefs/audio.
Editor-only heading font authoring persists TMP font asset and binds it to the prefab;
no fonts, widgets or hierarchy are created by runtime menu code.
Evidence: C:/Users/wkeif/Documents/Codex/2026-10-09/task-6/MenuValidationEvidence
Current authored validation files: authored-prefab-passed.txt or authored-prefab-error.txt.
Old error.txt records the previous validator failure, not a result for this repair.
Connected Game/Lobby/Boss Escape, real pointer blocking/cursor, transitions, standalone
screen mode and screenshots require a separate actual Play/build check.
Native milestone 2026-10-10 21:55:29 UTC: 14 actual authored-prefab/Awake/settings
assertions passed in the existing Unity editor; Creepster TMP asset was persisted.
Extended native validation now also uses an authored EditorOnly focus fixture.
Lifecycle readiness is fabricated without running a network operation; callbacks,
button events, session focus and enabled actions use production Unity components.
It checks Resume same-frame/held-click release, existing desk/intro focus, simulated
transition closure/reopen, and native imported overlay orders from isolated Game/Boss
preview scenes plus imported intro/how-to prefabs. It never opens/saves Lobby.
Read native-focus-layers-passed.txt for actual execution; absence means pending reload.
Neither this fixture nor the 14-assertion milestone establishes physical keyboard,
real peer gameplay clicks, or captured screenshot appearance.
