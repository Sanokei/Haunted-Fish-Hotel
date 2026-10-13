HAUNTED FISH HOTEL — INDOOR STORM RUNTIME PACK

START HERE
Use HFH_Rain_Indoor_Mix_Loop_60s.wav as the continuous rain bed. It is a 60-second layered loop with no deliberately added thunder. Four independent triggerable thunder assets are included; Distant_Long is a distance variation of Medium_Roll, while the other two use different recordings.

UNITY IMPORT AND MIX
• Rain: stereo; Force To Mono off; 48 kHz Preserve Sample Rate. For the first in-engine verification, PCM and Decompress On Load avoid a second lossy encode. Turn Preload Audio Data on. AudioSource.loop=true, spatialBlend=0, volume=1. Fade the source in over 1.5 s and out over 2 s; do not fade the loop's edges.
• Thunder: stereo; PCM; Decompress On Load; Preload Audio Data on; loop=false; playOnAwake=false; spatialBlend=0 unless intentionally placed near a window. Turn off Doppler for fixed environmental sources. Cue-level volume starting points are in thunder_cues.json.
• Route rain/thunder to an Ambience or Storm mixer group at 0 dB to begin. Reduce that group 3–6 dB during dialogue if needed. These assets have ample standalone peak room, but dialogue/music/UI were not available, so the whole-game mix is not mastered or certified.
• The individual 24-bit rain stems are retained separately; use either their sum or this premix. Do not stack the premix over its own stems.

LIGHTNING SYNCHRONIZATION
Hook the actual LightningEffectManager strike event, including the intro strike. Trigger once for a full strike group, not for each visual flicker. Remove/disable a separate random thunder timer so events cannot drift from lightning.
Use one DSP timestamp per lightning event. For each chosen cue:
clipStartDsp = strikeDsp + desiredAcousticDelaySeconds - onsetSeconds / pitch
At pitch 1, onsetSeconds is 0.060 for Medium_Roll and Soft_Crack, 0.085 for Distant_Long and 0.100 for Far_Rumble. These are approximate waveform-based onset offsets, not measured speaker latency. The sound begins near the start of each file; there is no multi-second baked-in distance delay.
Use AudioSource.PlayScheduled and enough lead time. If scheduling after an already-visible flash, select delays above 0.2 s and clamp the scheduled start at least 0.05 s ahead of current dspTime. Suggested acoustic delays are in thunder_cues.json and are art direction, not inferred physical window distances. Keep pitch=1 initially; if randomizing pitch, correct the onset offset and clip duration.
Current strikes 8–22 s apart may leave a low tail overlapping the next cue. Use a pool of at least two AudioSources, avoid retriggering an occupied source, and never double-fire the intro. Avoid immediately repeating the same cue. At most two live thunder voices are recommended.

WHAT WAS CHECKED
All exported runtime WAVs were decoded and measured for loudness, oversampled true peak, clipping, duration and boundary continuity. Every pair of thunder cues was mixed with rain at unity, 8 seconds apart. See audio_measurements.json for actual results. These are objective checks; no listening test was performed. Audition in the lobby on headphones and speakers before final approval. Short thunder-clip LUFS/LRA can be misleading because long tails are quiet; compare peaks and perceived impact too.

SOURCES AND LICENSE
All five source entries are CC0. Exact source/creator links, license links, source quality and SHA-256s are in LICENSES_AND_CREDITS.txt and sources.json. Some source material is lossy; the PCM exports are not lossless-source recordings.

OFFICIAL UNITY REFERENCES
https://docs.unity3d.com/ScriptReference/AudioSource.PlayScheduled.html
https://docs.unity3d.com/Manual/class-AudioClip.html
