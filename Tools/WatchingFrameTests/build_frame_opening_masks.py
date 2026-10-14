#!/usr/bin/env python3
"""Derive isolated opening masks from original empty frame alpha. Original art is never modified."""
import hashlib,json,re,sys
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/FrameArt'))
from segment_original_frames import folder,sprite_meta,guid,largest_region
ART=json.loads((ROOT/'Tools/FrameArt/frame-segmentation.json').read_text())
OUT=ROOT/'Assets/Art/Frames/Opening Masks'
def main():
    folder(OUT);items=[]
    for f in ART['frames']:
        original=np.asarray(Image.open(ROOT/f['variants']['empty']['path']).convert('RGBA'))
        alpha=original[:,:,3]
        inner=largest_region(ndimage.binary_fill_holes(alpha>0)&(alpha==0))
        # Preserve the actual closed border, while including tiny original specks inside its opening.
        inner=ndimage.binary_fill_holes(inner)
        pixels=np.full(original.shape,255,dtype=np.uint8);pixels[:,:,3]=inner.astype(np.uint8)*255
        p=OUT/f'Frame-Opening-{f["id"]:02d}.png';Image.fromarray(pixels).save(p);sprite_meta(p)
        mp=Path(str(p)+'.meta');text=mp.read_text();text=re.sub(r'  sRGBTexture: \d','  sRGBTexture: 0',text);mp.write_text(text)
        backing=ndimage.binary_dilation(inner,iterations=4) & ndimage.binary_erosion(ndimage.binary_fill_holes(alpha>0),iterations=1)
        backing |= inner
        bp=OUT/f'Frame-Backing-{f["id"]:02d}.png';back_pixels=pixels.copy();back_pixels[:,:,3]=backing.astype(np.uint8)*255;Image.fromarray(back_pixels).save(bp);sprite_meta(bp)
        bmp=Path(str(bp)+'.meta');bmp.write_text(re.sub(r'  sRGBTexture: \d','  sRGBTexture: 0',bmp.read_text()))
        items.append(dict(backing_path=str(bp.relative_to(ROOT)),backing_guid=guid(bp),frame_id=f['id'],path=str(p.relative_to(ROOT)),guid=guid(p),size=f['size'],pixels=int(inner.sum()),source=f['variants']['empty']['path'],sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    report=dict(schema=1,method='Largest alpha-enclosed opening of each exact original empty frame, with enclosed specks filled. Opaque backing extends four source pixels under partially transparent inner paint, bounded inside the original outer silhouette. Texture-local mask; no stencil IDs, SpriteMask, UI masks or global shader properties.',frames=items)
    (ROOT/'Tools/WatchingFrameTests/frame-opening-masks.json').write_text(json.dumps(report,indent=2)+'\n')
    print(f'PASS {len(items)} isolated original-frame opening masks authored')
if __name__=='__main__':main()
