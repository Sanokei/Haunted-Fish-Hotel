#!/usr/bin/env python3
"""Replay frame authoring without writes, requiring byte-exact final materials/geometry."""
from pathlib import Path
import sys,json,hashlib
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(ROOT/'Tools/WatchingFrameTests'))
import author_frame_galleries as galleries
import author_intro_frame_tunnel as extension
original_write=Path.write_text;original_unlink=Path.unlink;hashes={}
def verify_write(path,text,*args,**kwargs):
 path=Path(path);expected=text.encode(kwargs.get('encoding') or 'utf-8')
 if not path.exists():raise AssertionError('Missing authored file: '+str(path))
 if path.read_bytes()!=expected:raise AssertionError('Generator drift: '+str(path.relative_to(ROOT)))
 hashes[str(path.relative_to(ROOT))]=hashlib.sha256(expected).hexdigest();return len(text)
def no_unlink(path,*args,**kwargs):
 if Path(path).exists():raise AssertionError('Unexpected stale asset deletion: '+str(path))
try:
 Path.write_text=verify_write;Path.unlink=no_unlink;sys.argv=['verify_authoring_replay','--no-scene-writes'];galleries.main();extension.main()
finally:
 Path.write_text=original_write;Path.unlink=original_unlink
report={'passed':True,'scope':'No-write full generator replay against existing files; no scene writes; all emitted bytes match current source.','files':len(hashes),'sha256':hashes}
(ROOT/'Tools/WatchingFrameTests/eye-distribution-idempotence.json').write_text(json.dumps(report,indent=2)+'\n');print('PASS no-write frame/material replay:',len(hashes),'byte-exact files')
