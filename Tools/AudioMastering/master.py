"""Deterministic underwater hotel SFX mastering. Run from any directory."""
from pathlib import Path
import json, shutil
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, resample, fftconvolve, resample_poly

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
SR = 48000

def read(name):
    sr, x = wavfile.read(HERE / 'sources' / (name + '.wav'))
    assert sr == SR
    return x.astype(np.float64) / 32768

def master(x, pitch, cutoff, room, peak_db, rms_db):
    x = resample(x, round(len(x) / pitch))
    x -= x.mean()
    x = sosfilt(butter(2, 100, 'highpass', fs=SR, output='sos'), x)
    x = sosfilt(butter(3, cutoff, 'lowpass', fs=SR, output='sos'), x)
    # Gentle saturation rounds off harsh transients without flattening the action.
    x /= max(np.max(np.abs(x)), 1e-9)
    x = np.tanh(x * 1.3) / np.tanh(1.3)
    # Shared, diffuse short room; filtered reflections suggest flooded stone corridors.
    ir = np.zeros(round(SR * .32))
    for seconds, level in [(.023,.3),(.041,-.23),(.067,.17),(.097,-.12),(.139,.08),(.193,-.045),(.257,.022)]:
        ir[round(seconds*SR)] = level
    wet = fftconvolve(sosfilt(butter(2, 1800, fs=SR, output='sos'), x), ir)
    x = np.pad(x,(0,len(wet)-len(x))) + room * wet
    active = x[:max(1,len(x)-len(ir))]
    gain = min(10**(peak_db/20)/np.max(np.abs(x)), 10**(rms_db/20)/np.sqrt(np.mean(active**2)))
    x *= gain
    fade_in = min(round(SR*.002),len(x))
    fade_out = min(round(SR*.06),len(x))
    x[:fade_in] *= np.linspace(0,1,fade_in)
    x[-fade_out:] *= np.linspace(1,0,fade_out)
    return x

def master_swimming(x):
    # Preserve the accepted recording, soften its upper splash transients.
    x = sosfilt(butter(2, 70, 'highpass', fs=SR, output='sos'), x - x.mean())
    x = sosfilt(butter(2, 2200, 'lowpass', fs=SR, output='sos'), x)
    fade = round(SR*.035)
    x[:fade] *= np.linspace(0,1,fade)
    x[-fade:] *= np.linspace(1,0,fade)
    # Dense, dark reflections blend into a warm tail instead of discrete echoes.
    rng = np.random.default_rng(384218)
    t = np.arange(round(SR*.42)) / SR
    ir = rng.normal(size=len(t)) * np.exp(-t*16)
    ir[:round(SR*.012)] = 0
    ir = sosfilt(butter(2, 1300, 'lowpass', fs=SR, output='sos'), ir)
    ir /= np.sqrt(np.sum(ir*ir))
    wet = fftconvolve(x, ir)
    # Balance wet energy explicitly so the quiet tail never overwhelms the stroke.
    wet *= .18 * np.linalg.norm(x) / max(np.linalg.norm(wet),1e-12)
    y = np.pad(x,(0,len(wet)-len(x))) + wet
    y *= min(10**(-14/20)/np.max(np.abs(y)),10**(-30/20)/np.sqrt(np.mean(y[:len(x)]**2)))
    y[-round(SR*.06):] *= np.linspace(1,0,round(SR*.06))
    return y

configs = [
    ('FishWalk01','swimming',24.20,24.68,1,0,0,-12,-29),
    ('FishWalk02','swimming',25.80,26.28,1,0,0,-12,-29),
    ('FrontDeskDing','bell',0,4.8,.86,3800,.32,-5,-22),
    ('FrontDeskClick','switch',0,.205,.90,3000,.20,-8,-25),
]
report = {}
clips = {}
for name, src, start, end, pitch, cutoff, room, peak_db, rms_db in configs:
    target = ROOT / 'Assets/Art' / (name + '.wav')
    backup = HERE / 'originals' / target.name
    backup.parent.mkdir(exist_ok=True)
    if not backup.exists(): shutil.copy2(target,backup)
    excerpt = read(src)[round(start*SR):round(end*SR)]
    x = master_swimming(excerpt) if src == 'swimming' else master(excerpt,pitch,cutoff,room,peak_db,rms_db)
    wavfile.write(target,SR,np.round(x*32767).astype(np.int16))
    true_peak = np.max(np.abs(resample_poly(x,4,1)))
    assert true_peak < .95 and np.isfinite(x).all()
    report[name] = dict(seconds=round(len(x)/SR,3),peak_dbfs=round(20*np.log10(np.max(np.abs(x))),2),true_peak_dbtp=round(20*np.log10(true_peak),2),rms_dbfs=round(20*np.log10(np.sqrt(np.mean(x*x))),2),sample_rate=SR,channels=1)
    clips[name] = x

# Preserve overlapping tails at the actual movement cadence.
swimming = np.zeros(round(SR*3.8))
for i in range(8):
    x = clips['FishWalk01' if i%2 == 0 else 'FishWalk02']*.3
    start = round(i*SR*.4)
    swimming[start:start+len(x)] += x
wavfile.write(HERE/'Swimming-preview.wav',SR,(swimming*32767).astype(np.int16))
sequence = [swimming]
sequence += [np.zeros(SR),clips['FrontDeskDing'],np.zeros(SR),clips['FrontDeskClick'],np.zeros(SR//2),clips['FrontDeskClick']]
wavfile.write(HERE/'UnderwaterHotel-preview.wav',SR,(np.concatenate(sequence)*32767).astype(np.int16))
(HERE/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
