# Dialogue regression checks

Run `dotnet run --project Tools/DialogueTests/DialogueTests.csproj` from the repository root.

This executable links the production dialogue manager, Ink session, variable store,
panel, option, and group components with the project's vendored Ink compiler/runtime.
Small doubles cover only Unity UI/input and adjacent network/story-effect services.
The scenarios check variable scope, observer cleanup, input suspension without effect
replay, actual Ink choice branching, option ownership, repeated enter/exit, manager
disable, and remote snapshot presentation without local story execution.
Foreign, retired, closed, hidden, and inactive choice rows are also checked against
a live manager to ensure they cannot advance its story or route shared requests.
Choice-only knots remain active while awaiting selection. Component interruption
closes owned input and emits one end event so shared presentation can become inactive.

The executable checks do not verify Unity lifecycle scheduling, prefab references,
rendered layout, or Mirage transport.

## Native Unity checks

`RunUnity.ps1 -ValidationProject <existing cached full project under personal Temp>`
copies current production scripts and the authored dialogue/input/option prefabs into
that disposable project. It launches a hidden Editor, enters an empty test scene, and
runs `NativeDialogueValidation.cs`. Use `-PrepareOnly` to sync without launching, or
`-StartOnly` to return the Editor PID and log path for separate monitoring.

The native runner checks real option ownership and deferred destruction, choice-only
Ink knots, scoped variables, actual InputText submission, and disable/destroy cleanup.
It writes `DialogueValidationEvidence/result.txt` in the validation copy. The script
refuses to sync a project already open in an Editor or a project outside personal Temp.
It never launches the shared working project. The native checks do not prove rendered
layout or Mirage transport.
