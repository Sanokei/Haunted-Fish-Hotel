from pathlib import Path
import hashlib,json,re
root=Path(__file__).resolve().parents[2]
e=root/'Tools/PlayerTests/Evidence/2026-10-10-jump'
for p,h in json.loads((e/'final-source-hashes.json').read_text()).items():
    assert hashlib.sha256((root/p).read_bytes()).hexdigest()==h, f'Native evidence stale: {p}; rerun manual JumpNativeValidation.Run'
t=(e/'native-final-cart.txt').read_text()
assert 'PHYSICS basket=1.700 handle=2.000 solidParts=2' in t
rows=re.findall(r'jump=10.48 gravity=25 speed=([\d.]+) direction=(-?1) approaching=([05]): (\d+)/102 native crossings',t)
assert len(rows)==8 and all(int(r[3])>=5 for r in rows)
assert 'PASS 26314 native edit-mode checks' in t
assert 'noDuplicate=True grace=True boss=True clearAbove=True' in t
print('PASS recorded native evidence matches current six production files; eight traversal cases, ceiling/gates/damage. This does not rerun Unity or certify Play Mode.')
