# Lobby storm audio verification and scene handoff

Run: dotnet run --project Tools/StormAudioTests/StormAudioTests.csproj
Final result: 55 checks passed. Intro/door/setup regression: 60 checks passed.

Tests compile and execute production LobbyStormAudio and LobbyStormAudioState with
lightweight Unity/audio/light-event doubles. They cover delay/onset/pitch compensation,
two-voice DSP scheduling, occupied-voice protection, retained rapid strikes, duplicate
suppression, weighted choice without immediate repeats, cue gains, dialogue ducking,
rain fades, stop/disable/unsubscribe and reactivation. Source assertions retain the
actual lightning coroutine's one event, white-frame count and original light envelope.
These tests do not establish native Unity compilation or audible listener output.

Downloaded local package: C:/Users/wkeif/Downloads/HauntedFishHotel_IndoorStorm_Runtime.zip
ZIP size: 14959022 bytes. ZIP CRC check passed. Every imported WAV matches the SHA-256
in the package audio_measurements.json and parses as stereo 48000 Hz 16-bit PCM.
The audition MP3 was not imported. Library transfer failure is superseded by the
user's explicit local download authorization.

Imported runtime WAVs under Assets/Art/Storm:
- HFH_Rain_Indoor_Mix_Loop_60s.wav: 14632f621260414288843ac222051b10
- HFH_Thunder_Medium_Roll.wav: 7294bc921fce433180449d2cedd92776
- HFH_Thunder_Soft_Crack.wav: df15f064a5de4e289ab0be6167dac1e0
- HFH_Thunder_Distant_Long.wav: 95e71cd93e51446d9c6b3fd4fc718825
- HFH_Thunder_Far_Rumble.wav: 312c874d61ce4447bca9fa3d0f15b5a4
AudioClip scene references use fileID 8300000, guid above, type 3.
Importer settings: stereo preserved, PCM, Decompress On Load, preload enabled,
sample rate preserved, normalization disabled. Confirm these in the native Inspector.

CC0 credits, source links/hashes, package recommendations and measured levels are
preserved byte-for-byte under Assets/ThirdParty/IndoorStorm:
LICENSES_AND_CREDITS.txt, README.txt, sources.json, thunder_cues.json, audio_measurements.json.
Credits include alxl/Ylmir rain and seth-m/cognito perceptu/TRP thunder.
No new source downloads, normalizing, limiting or waveform edits were performed.

Scene owner setup (this task has not edited Lobby.unity):
Component: HauntedFish.Multiplayer.LobbyStormAudio
Script: Assets/Scripts/Multiplayer/Lobby/LobbyStormAudio.cs
MonoScript GUID: f90c723e4c19415fa27871278122e4ce
- _Lightning: existing LightningEffectManager.
- _RainSource: dedicated stereo 2D AudioSource.
- _ThunderSource and _ThunderSourceB: two distinct dedicated stereo 2D AudioSources.
  One-source fallback remains compatible but cannot preserve overlapping tails as well.
- All three sources: Play On Awake off, Doppler 0, pitch 1.
- _RainLoop: rain WAV above.
- _ThunderClips: Medium_Roll, Soft_Crack, Distant_Long, Far_Rumble in this exact order.
- _ThunderOnsetSeconds: [.060, .060, .085, .100].
- _ThunderCueGains: [.8, .7, 1, 1].
- _ThunderCueDelays: [(1,2.2),(.6,1.3),(2.2,3.8),(1.8,3.2)] seconds.
- _ThunderCueWeights: [3,2,3,3].
- _RainGain=1, _ThunderGain=1, _ThunderGainVariation=0.
  These supersede earlier .16/.42 and .65/.42 starting values. The package is already quiet.
- _ThunderDelaySeconds=(.5,1.8) is fallback only for a cue without per-cue metadata.
- _SchedulingLeadSeconds=.05; _RainFadeInSeconds=1.5; _RainFadeOutSeconds=2.
- _RainDuringDialogue=.55; _ThunderDuringDialogue=.65; _GainFadeSeconds=.5.
- Use an Ambience/Storm mixer group at 0 dB if available. Do not reuse music/speech sources.

Lifecycle:
LightningEffectManager.Play() starts ambience and emits one StrikeStarted(long id)
at the actual strike coroutine onset. StartAmbient() starts rain without inventing a
strike. Its original ambient random timer remains the only lightning timer.
Each event captures AudioSettings.dspTime. Cue playback is scheduled at
max(strike DSP + distance delay - onset/pitch, current DSP + .05).
Both voices share positive constant pitch, initially 1. Keep pitch unchanged while cues
are scheduled/playing. A busy source is never retriggered; excess rapid strikes remain
queued until a voice is free, then receive safe scheduling lead time.
Dialogue start/end smoothly changes both dedicated source gains.
Light-owner disable cancels scheduled and queued thunder, fading rain out over 2 seconds
while the audio adapter remains active. Adapter disable/scene cleanup immediately stops
rain and thunder, cancels fades/queued events, and detaches all subscriptions.
Re-enable reads current light ambience and dialogue state, allowing a fresh lifetime.

Required native integration/audition:
1. Run intro; confirm rain starts at its existing final Play() and one thunder follows.
2. Observe real window strikes and confirm audible delays/onset from both voices.
3. Rapid Play calls must not double-fire or cut an occupied voice's tail.
4. Begin/end Grandma dialogue; listen to duck/recovery with music and all game effects.
5. Disable/re-enable or leave scene while audio is scheduled; confirm no stale playback.
6. Audition headphones and speakers. Package objective overlap peak is -9.4 dBFS at unity,
   but whole-game mix and perceived character have not been listened to or certified.
