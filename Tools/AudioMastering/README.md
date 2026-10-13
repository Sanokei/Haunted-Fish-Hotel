# Underwater hotel sound pass

Run `python Tools/AudioMastering/master.py` to rebuild the four existing Unity WAV assets and the combined preview. Requires NumPy and SciPy. `download_sources.py` fetches the public HQ MP3 previews and decodes with ffmpeg; source URLs and licenses are recorded beside the downloads. These are preview-derived masters, not lossless original recordings.

Movement uses Felix Blume's real underwater hydrophone recording of hand-driven water movement, bubbles and drops (Freesound 384218, CC0, 2017-03-14). Two excerpts at 24.20-24.68s and 25.80-26.28s replace the rejected generated sounds. Processing includes 70 Hz rumble removal, a 2.2 kHz low-pass to soften splash transients, and subtle diffuse reverb with a 0.42 second tail filtered at 1.3 kHz. Wet energy is 18% of dry energy; the active stroke targets -30 dBFS RMS with a -14 dBFS peak ceiling. Edge fades prevent cut clicks. No generated tones, pitch change or saturation are used for swimming. Existing .3 movement playback gain is preserved. The bell and switch retain their previous mastering.

Mono 48 kHz / 16-bit PCM keeps positional playback compatible with current Unity AudioSources. Existing filenames and metadata GUIDs are preserved. `verification.json` records duration, RMS, sample and oversampled true peaks. Originals are backed up once in `originals/`.

`Swimming-preview.wav` presents eight swimming strokes at the game's .3 gain and .4 second cadence, with overlapping tails. `UnderwaterHotel-preview.wav` follows that with a bell and two switch clicks. Signal checks passed; listening balance in the running Unity scene remains to be assessed. This pass does not change dialogue, music, jump, or interaction sounds.
