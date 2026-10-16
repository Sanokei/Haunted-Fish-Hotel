# Lobby Intro / Theatre audio sources

All sources come from Wikimedia Commons. Every file is Public Domain or CC0, except `LobbyIntro_Thunder_02` and `LobbyIntro_LockUnlock`, which are CC BY 4.0 and need a credit line.
All files are mastered as 48 kHz stereo Ogg Vorbis (q6). Mastering steps:
- trimmed to the segment used, with a high-pass rumble filter where noted
- short fades on one-shots
- an equal-power tail-into-head crossfade on loops, so they wrap seamlessly
- a single linear gain to the loudness target (no compression), with peaks capped at -1.5 dBTP

The loudness targets are -16 LUFS for loops and music and -14 LUFS for one-shots. For transient-heavy one-shots, the peak cap wins, so they measure quieter than -14. Adjust per-source volume in Unity.

| File | Use | Source (Wikimedia Commons) | Author | License | Segment / notes |
|---|---|---|---|---|---|
| LobbyIntro_CityRain_Loop.ogg | City rain bed, seamless 39 s loop | https://commons.wikimedia.org/wiki/File:Rain_(1).ogg | ezwa | Public domain | 2–44 s, HP 40 Hz, 3 s loop crossfade |
| LobbyIntro_Thunder_01.ogg | Thunder strike + roll | https://commons.wikimedia.org/wiki/File:Rain_and_thunder.ogg | User:Caesar | Public domain | 2.2–15 s |
| LobbyIntro_Thunder_02.ogg | Sharp thunderclap (biggest) | https://commons.wikimedia.org/wiki/File:Nosferatu_thunderclap_-_Richard_Humphries.wav | Richard Humphries | CC BY 4.0 (credit required) | 2.9–22 s |
| LobbyIntro_Thunder_03.ogg | Thunder strike over rain | https://commons.wikimedia.org/wiki/File:Rain_thunder_steps.ogg | ezwa | Public domain | 8.3–22 s |
| LobbyIntro_Thunder_04.ogg | Distant rolling thunder | https://commons.wikimedia.org/wiki/File:Rain_thunder_steps.ogg | ezwa | Public domain | 56.5–72 s |
| LobbyIntro_DoorKnock.ogg | 3 knocks, wooden door | https://commons.wikimedia.org/wiki/File:Knocking_on_wood_or_door.ogg | stephan | Public domain | 0.15–1.45 s (first group of 3) |
| LobbyIntro_DoorKnock_Alt.ogg | Alternate 3-knock pattern (lighter) | https://commons.wikimedia.org/wiki/File:Knock_on_door.wav | Asud12 | CC0 | 1.2–2.7 s, HP 60 Hz |
| LobbyIntro_HotelExterior_Loop.ogg | Hotel exterior: wind, rain on metal/glass, distant creaks; 30 s loop | Wind: https://commons.wikimedia.org/wiki/File:Killiney_Hill_Storm_Floris_20250804_1537.ogg · Rain: https://commons.wikimedia.org/wiki/File:Rain_against_the_window.ogg · Creaks: https://commons.wikimedia.org/wiki/File:Door_handle_creaking.ogg , https://commons.wikimedia.org/wiki/File:LL-Q1860_(eng)-Wodencafe-creakings.wav | Karlunun (wind, CC0); cori (rain, PD); stephan (creak, PD); Wodencafe (creak, CC0) | CC0 / Public domain | wind 4–36 s + rain 30–62 s, creaks at 9 s and 21 s, 2 s loop crossfade |
| Theatre_MapleLeafRag_Joplin1916.ogg | Theatre / silent-film saloon piano | https://commons.wikimedia.org/wiki/File:Maple_Leaf_Rag_-_played_by_Scott_Joplin_1916_sample.ogg | Scott Joplin (1916 piano roll performance) | Public domain | full 130 s, silence trimmed, 1.5 s tail fade; not a seamless loop (ends on the cadence) |

## Authored one-shots (Source/author_lobby_sfx.py)
Rendered by `python -I Source/author_lobby_sfx.py <sources dir> <out dir>` from the files below. Clips under 0.4 s can't be gated by an EBU R128 meter, so these are peak-normalised and pushed into a -1 dB limiter instead of loudness-normalised. They land at about -10 to -14 LUFS max-momentary, which sits over the organ bed (about -9 LUFS max-momentary).

| File | Use | Source (Wikimedia Commons) | Author | License | How it's built |
|---|---|---|---|---|---|
| LobbyIntro_DoorKnock_Single.ogg | One knock, played per Knock() hit | https://commons.wikimedia.org/wiki/File:Knocking_on_wood_or_door.ogg | stephan | Public domain | first knock 0.20–0.50 s, body/attack EQ, short room, +8 dB into limiter |
| LobbyIntro_EnvelopeSlide.ogg | Envelope sliding under the door | https://commons.wikimedia.org/wiki/File:Book_paper_pages_assorted.ogg | stephan | Public domain | synthesised paper friction (band-limited noise, swell, stick-slip wobble) over a quiet 55 s source texture; 1.6 s |
| LobbyIntro_LetterRustle.ogg | Third panel slides in: tear, then paper rustle | https://commons.wikimedia.org/wiki/File:Book_paper_pages_assorted.ogg | stephan | Public domain | 67.65–72.25 s; tear in the first second lifted 4 dB, rustle follows |
| LobbyIntro_LockUnlock.ogg | Door lock: key/lever ticks, then the bolt clunks back | https://commons.wikimedia.org/wiki/File:Lock_(Gravity_Sound).wav , https://commons.wikimedia.org/wiki/File:Tight_door_lock_(Gravity_Sound).wav | Gravity Sound | CC BY 4.0 | ticks 0–0.375 s, then clunk 0.28–1.28 s at 0.42 s; natural speed, light EQ and room, +14 dB into limiter; 1.2 s |
| LobbyIntro_GateSqueak.ogg | Iron gate hinge squeal as it swings open | https://commons.wikimedia.org/wiki/File:Son_de_l%27ouverture_et_fermeture_du_portail_du_cimeti%C3%A8re_du_Mas_Rillier.ogg | Benoît Prieur | CC0 | squeal only, 1.55–6.15 s; 4.6 s, played in full before the door opens |

Required credit (for game credits):
"Thunderclap" by Richard Humphries, CC BY 4.0, via Wikimedia Commons.
"lock" and "tight door lock" by Gravity Sound, CC BY 4.0, via Wikimedia Commons.

Organ track: none found. %TEMP%, ~/.codex (sessions and .tmp) and AppData have no organ audio for this project. The Codex session hits for "organ" were the word "organize", and the Sonic Pi temp renders are sub-second blips or are from another project (onlineslop).

## LobbyIntro_Organ.ogg
Original composition for this project, performed and recorded natively in Sonic Pi 5.0.0. Score: `Source/lobby_intro_organ.rb`, cued to the timeline in `Assets/Dialogue/LobbyIntroduction.json` (t=0 is the first storm frame; lightning stab 1.5s, knocks 2.3s, letter 4.15s, exterior flash 19.25s, lobby 26.25s, tunnel 40.25s, final chord 46.25s). No samples or existing melodies used.
