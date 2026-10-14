#!/usr/bin/env python3
"""Source-only QA for the current capsule exports and their Unity metadata."""
from pathlib import Path
import hashlib,json,re,sys
from PIL import Image
ROOT=Path(__file__).resolve().parents[2];DIR=ROOT/'Assets/Art/Capsule'
def check(condition,message):
 if not condition:raise AssertionError(message)
def main():
 p=json.loads((DIR/'Current-Main-Provenance.json').read_text());guids=[];checks=0
 for f in p['exports']:
  path=DIR/f['file'];im=Image.open(path)
  check(im.size==(1232,706),f'{path.name}: exact main size');checks+=1
  check(im.mode in ('RGB','RGBA'),f'{path.name}: RGB color');checks+=1
  check(im.mode!='RGBA' or im.getchannel('A').getextrema()==(255,255),f'{path.name}: opaque');checks+=1
  check(hashlib.sha256(path.read_bytes()).hexdigest()==f['sha256'],f'{path.name}: provenance SHA256');checks+=1
  meta=path.with_suffix(path.suffix+'.meta').read_text()
  for needle in ['enableMipMap: 0','sRGBTexture: 1','nPOTScale: 0','textureType: 0','textureCompression: 0']:check(needle in meta,f'{path.name}: {needle}');checks+=1
  guids.append(re.search(r'^guid: ([a-f0-9]{32})$',meta,re.M).group(1))
 for path in [DIR,DIR/'README.txt',DIR/'Current-Main-Provenance.json']:
  meta=path.with_suffix(path.suffix+'.meta').read_text();guids.append(re.search(r'^guid: ([a-f0-9]{32})$',meta,re.M).group(1));checks+=1
 check(len(set(guids))==len(guids),'Unique capsule GUIDs');checks+=1
 occurrences={g:[] for g in guids}
 for path in (ROOT/'Assets').rglob('*.meta'):
  text=path.read_text(errors='replace');m=re.search(r'^guid: ([a-f0-9]{32})$',text,re.M)
  if m and m.group(1) in occurrences:occurrences[m.group(1)].append(str(path.relative_to(ROOT)))
 check(all(len(v)==1 for v in occurrences.values()),'No capsule GUID collides with another asset');checks+=1
 check(p['status']=='main composition review; remaining formats pending acceptance','Correct review status');checks+=1
 check(p['art_only_is_store_ready'] is False,'Art-only is not represented as a compliant Steam capsule');checks+=1
 check(not list(DIR.glob('*.blend')),'No oversized Blender binary inside Unity Art/Capsule');checks+=1
 for path in p['original_art_sources']:check((ROOT/path).is_file(),'Original source exists: '+path);checks+=1
 print(json.dumps({'passed':True,'checks':checks,'revision':p['revision'],'exports':len(p['exports']),'native_unity_import_run':False},indent=2))
if __name__=='__main__':main()
