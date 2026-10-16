# Authors the lobby intro one-shots from the Wikimedia Commons sources listed in SOURCES.md.
# Usage: python -I author_lobby_sfx.py <downloaded sources dir> <output dir>
# Needs numpy and ffmpeg on PATH.
import subprocess
import sys
from pathlib import Path

import numpy as np

SR = 48000
src_dir, out_dir = Path(sys.argv[1]), Path(sys.argv[2])
rng = np.random.default_rng(7)


def load(name, ss=0.0, t=None, rate=1.0):
    # rate < 1 slows and pitches down (tape style)
    args = ["ffmpeg", "-v", "error", "-ss", str(ss), "-i", str(src_dir / name)]
    if t:
        args += ["-t", str(t)]
    af = f"asetrate={int(SR * rate)},aresample={SR}" if rate != 1.0 else f"aresample={SR}"
    args += ["-af", af, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"]
    return np.frombuffer(subprocess.run(args, capture_output=True, check=True).stdout, np.float32).copy()


def fade(x, fin=0.004, fout=0.05):
    n_in, n_out = int(fin * SR), int(fout * SR)
    x[:n_in] *= np.linspace(0, 1, n_in)
    x[-n_out:] *= np.linspace(1, 0, n_out) ** 2
    return x


def place(bed, clip, at, gain=1.0):
    i = int(at * SR)
    end = min(len(bed), i + len(clip))
    bed[i:end] += clip[: end - i] * gain
    return bed


def onepole_lp(x, hz):
    a = np.exp(-2 * np.pi * hz / SR)
    y = np.empty_like(x)
    s = 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y


def room(x, size=0.18, mix=0.18):
    # a few sparse decaying reflections; enough to put a dry close-mic in a hallway
    y = x.copy()
    for d, g in [(0.011, 0.5), (0.023, 0.38), (0.037, 0.3), (0.061, 0.2), (0.089, 0.12)]:
        y = place(y, x * g * mix / 0.5, d * size / 0.18)
    return y


def master(x, name, drive=0.0, ceiling=-1.0, filters=""):
    # loudnorm can't gate clips this short, so: filter, peak-normalise, then push `drive` dB into a
    # limiter at the ceiling. Drive was tuned by measuring short-term loudness against the organ bed.
    raw = out_dir / f"_{name}.f32"
    if filters:
        x = np.frombuffer(subprocess.run(
            ["ffmpeg", "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-af", filters,
             "-f", "f32le", "-"], input=x.astype(np.float32).tobytes(), capture_output=True, check=True).stdout,
            np.float32)
    (x / np.abs(x).max()).astype(np.float32).tofile(raw)
    chain = (
        f"volume={drive}dB,"
        f"alimiter=limit={10 ** (ceiling / 20):.4f}:attack=0.5:release=30:level=false,"
        "aresample=48000"
    )
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", str(raw),
                    "-af", chain, "-ac", "2", "-c:a", "libvorbis", "-q:a", "6", str(out_dir / f"{name}.ogg")], check=True)
    raw.unlink()


# Knock: one hard wooden knock, close and weighty so it reads under the organ.
knock = fade(load("Knocking_on_wood_or_door.ogg", 0.20, 0.30), 0.001, 0.08)
knock = room(knock, mix=0.25)
master(knock, "LobbyIntro_DoorKnock_Single", drive=8.0,
       filters="highpass=f=70,equalizer=f=180:t=q:w=1:g=5,equalizer=f=2500:t=q:w=1.2:g=3")

# Paper slide: an envelope pushed across a floorboard. Friction noise band-limited to paper's
# 1-7 kHz hiss, a slow swell with tiny stick-slip wobble, layered under a quiet real paper texture.
dur = 1.6
n = int(dur * SR)
t = np.arange(n) / SR
noise = rng.standard_normal(n).astype(np.float32)
hiss = onepole_lp(noise, 6500) - onepole_lp(noise, 900)
body = onepole_lp(noise, 700) - onepole_lp(noise, 180)
env = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 0.7 * (0.75 + 0.25 * np.sin(2 * np.pi * 0.9 * t))
wobble = 1 + 0.18 * onepole_lp(rng.standard_normal(n).astype(np.float32), 25) * 8
slide = (hiss * 0.9 + body * 0.35) * env * wobble
texture = load("Book_paper_pages_assorted.ogg", 55.0, dur)[:n]
slide = place(slide, texture * 0.35, 0)
slide = fade(slide, 0.06, 0.25)
master(slide, "LobbyIntro_EnvelopeSlide", drive=-3.0, filters="highpass=f=150")

# Letter: the envelope tears open, then the letter rustles out. The source tears at 67.7 s and
# rustles straight on, so it is one continuous cut with the tear pushed forward.
letter = load("Book_paper_pages_assorted.ogg", 67.65, 4.6)
tear_end = int(1.0 * SR)
letter[:tear_end] *= 1.6
letter = fade(letter, 0.005, 0.6)
master(letter, "LobbyIntro_LetterRustle", drive=3.0, filters="highpass=f=90,equalizer=f=4500:t=q:w=1:g=2")

# Unlock: real door-lock recordings at natural speed. Key/lever ticks, then the bolt clunks back.
ticks = fade(load("Lock_Gravity_Sound.wav", 0.0, 0.375), 0.002, 0.03)
clunk = fade(load("Tight_door_lock_Gravity_Sound.wav", 0.28, 1.0), 0.001, 0.3)
unlock = np.zeros(int(1.2 * SR), np.float32)
unlock = place(unlock, ticks, 0.0, 1.0)
unlock = place(unlock, clunk, 0.42, 1.0)
unlock = fade(room(unlock, mix=0.12), 0.002, 0.25)
master(unlock, "LobbyIntro_LockUnlock", drive=14.0, filters="highpass=f=70,equalizer=f=250:t=q:w=1:g=2,equalizer=f=4500:t=q:w=1.5:g=-2")

# Gate: wrought-iron cemetery gate, hinge squeal only as it swings open.
gate_src = "Son_de_l_ouverture_et_fermeture_du_portail_du_cimeti_re_du_Mas_Rillier.ogg"
gate = fade(load(gate_src, 1.55, 4.6), 0.05, 1.2)
master(gate, "LobbyIntro_GateSqueak", drive=0.0, filters="highpass=f=60")
