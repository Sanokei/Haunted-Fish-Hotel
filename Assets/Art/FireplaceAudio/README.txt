HAUNTED FISH HOTEL • LOBBY FIREPLACE
Prepared 11 October 2026

READY-TO-IMPORT AUDIO
HFH_Lobby_Fireplace_GentleLoop_60s_Mono48k.wav
60 seconds, mono, 48 kHz, 24-bit PCM WAV.
A restrained fireplace layer intended to sit locally at the Lobby hearth.
The WAV is the looping asset. It has no fade-to-silence at its boundary.

HFH_Fireplace_Audition_20s.mp3
A short listening preview containing the last 10 seconds and first 10 seconds
of the master. The loop wrap occurs at 10 seconds. Preview-only fades are at
its beginning/end. Do not use this MP3 as the game's looping asset.

UNITY STARTING POINT
1. Drag the WAV into your project's audio folder and assign it to an AudioSource
   located inside the fireplace opening.
2. Enable Loop. Enable Play On Awake only if the Lobby should always start it.
3. Set Spatial Blend to 1 (3D), Doppler Level to 0, and Pitch to 1.
4. Begin with Volume around 0.4, then balance against footsteps, dialogue,
   and the room ambience at the usual player location. This is a mix suggestion.
5. Use a custom volume rolloff curve, with full level near the hearth, a quiet
   tail across the Lobby, and zero outside its intended range. Set distances
   to the actual scene scale; avoid assuming one room size fits every scene.
6. The file is already mono. Preserve 48 kHz. For a first seam check, use PCM
   compression and Decompress On Load. Optimise import settings for the target
   platform after testing; compressed/platform-transcoded playback needs a
   fresh loop-boundary check.
7. Add any room reverb in Unity so it matches the Lobby. No reverb was baked in.

Check at least two full loop repetitions in headphones, then walk toward and
away from the hearth in the game. Keep the fire beneath important cues.
No Unity scene or user-computer files were edited or tested.

HOW THIS VERSION WAS MADE
Source: Joseph SARDIN's Fireplace #4 on BigSoundBank (sound 2856), a real indoor
fireplace recording. The downloaded source was the site's 192 kbps, stereo,
48 kHz MP3. This PCM WAV avoids adding another lossy encode in the master;
it does not restore information already lost in the source MP3.

The master combines source regions 5-35 seconds and 44-78 seconds, avoiding
an especially large transient in the omitted region. Stereo was averaged to
mono. An 85 Hz high-pass and 6.5 kHz low-pass reduce rumble and sharpness.
Two 2-second equal-power crossfades join the regions and create the wrap.
A look-ahead, soft-knee 3:1 transient treatment attenuates stronger cracks,
followed by restrained level adjustment and a smooth -12 dBFS peak ceiling.
No music, wind, speech, synthetic crackle, or reverb was added.

VALIDATION
The audio decoded successfully. Duration, sample rate, channel count,
headroom, DC level, and the sample transition at the loop boundary were
checked. Exact measurements and checksums are in Audio_Technical_Checks.json.
Perceptual listening and in-engine playback were not available during
preparation; please audition for tone and remaining natural wood pops before
committing the sound to the game. Numerical checks cannot establish taste,
absence of unwanted sounds, or seamless playback in every target build.

SOURCE / LICENSE
See SOURCE_AND_LICENSE.txt for the original recording and reuse terms.

Unity AudioSource reference:
https://docs.unity.com/en-us/engine/6000.0/manual/audio/reference/class-audio-source/source-reference
