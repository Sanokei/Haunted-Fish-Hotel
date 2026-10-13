Theatre guides use the complete Fish/Ghost mechanics text from `Assets/Dialogue/TutorialRecording/*Recording.ink`, presented as full-screen silent intertitles. Gameplay footage has its baked caption region and role label removed and is letterboxed without stretching. No text overlaps the gameplay shots. Cards allow approximately 2.7 words per second plus one second, with a 3.5-second minimum.

Rebuild with `python Tools/TheatreVideos/build_intertitles.py`. Requires Pillow and FFmpeg; `--sources` and `--ffmpeg` override the archived footage directory and executable. The existing MP4 asset paths and Unity meta files are preserved, so theatre and other consumers retain their bindings. Both outputs are fully decoded before replacement. `Evidence/manifest.json` records the chapter text and input sources.

The archived Ghost recording ends during the chandelier drop. Reset, click trap, discard, role swap and readiness instructions are all restored as cards; those later demonstrations were not present in the archived recording. Unity playback has not been rechecked in the live editor.

Radio validation: `dotnet run --project Tools/RadioTests/RadioTests.csproj` exercises production playback using deterministic audio stubs.
