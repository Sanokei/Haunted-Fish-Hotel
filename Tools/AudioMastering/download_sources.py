from pathlib import Path
import urllib.request, re, json, subprocess, sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Tools/AudioMastering/sources'
OUT.mkdir(parents=True, exist_ok=True)
sources = [('steps','SpliceSound',338106), ('bell','FlashTrauma',398281), ('switch','LoafDV',238290), ('swimming','felix.blume',384218)]
if len(sys.argv) > 1:
    sources = [s for s in sources if s[0] == sys.argv[1]]
for name, author, ident in sources:
    url = f'https://freesound.org/people/{author}/sounds/{ident}/'
    html = urllib.request.urlopen(url, timeout=30).read().decode()
    previews = sorted(set(re.findall(r'https[^\s"<>]+-hq\.mp3',html)))
    if not previews:
        raise RuntimeError(f'No public HQ preview for {url}')
    if name == 'swimming':
        # Only the first 30 seconds are needed from this ten-minute recording.
        source = str(OUT/f'{name}.mp3') if (OUT/f'{name}.mp3').exists() else previews[0]
        subprocess.run(['ffmpeg','-v','error','-y','-i',source,'-t','30','-ar','48000','-ac','1',str(OUT/f'{name}.wav')],check=True)
    else:
        urllib.request.urlretrieve(previews[0],OUT / f'{name}.mp3')
        subprocess.run(['ffmpeg','-v','error','-y','-i',str(OUT/f'{name}.mp3'),'-ar','48000','-ac','1',str(OUT/f'{name}.wav')],check=True)
    (OUT/f'{name}.json').write_text(json.dumps(dict(author=author,url=url,preview=previews[0],license='CC0',excerpt_seconds=30 if name == 'swimming' else None),indent=2))
    print(name, previews[0])
