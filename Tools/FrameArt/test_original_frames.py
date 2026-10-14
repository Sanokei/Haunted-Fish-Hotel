#!/usr/bin/env python3
"""Independent checks against retained originals and authored Unity metadata."""
from pathlib import Path
import hashlib,json,re,unittest
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT=Path(__file__).resolve().parents[2]
MAN=json.loads((ROOT/'Tools/FrameArt/frame-segmentation.json').read_text())

def arr(path): return np.array(Image.open(ROOT/path).convert('RGBA'))
def digest(path): return hashlib.sha256((ROOT/path).read_bytes()).hexdigest()

class OriginalFrameTests(unittest.TestCase):
    def test_sources_remain_original(self):
        self.assertEqual(4,len(MAN['sources']))
        for source in MAN['sources']:
            self.assertEqual(source['sha256'],digest(source['path']))
            with Image.open(ROOT/source['path']) as im:
                self.assertEqual(source['size'],list(im.size))

    def test_all_50_frame_segments_are_source_pixels(self):
        filled=arr('Assets/Art/Frames/Originals/Frames-Filled-00.png')
        empty=arr('Assets/Art/Frames/Originals/Frames-Empty-00.png')
        labels,_=ndimage.label(filled[:,:,3]>0,np.ones((3,3)))
        counts=np.bincount(labels.ravel());boxes=ndimage.find_objects(labels)
        ids=[int(x) for x in np.where(counts>500)[0] if x]
        ids.sort(key=lambda i:(boxes[i-1][0].start,boxes[i-1][1].start))
        major=np.isin(labels,ids)
        near=ndimage.distance_transform_edt(~major,return_distances=False,return_indices=True)
        owner=labels[near[0],near[1]]
        self.assertEqual(25,len(ids));self.assertEqual(25,len(MAN['frames']))
        for frame,component in zip(MAN['frames'],ids):
            l,t,r,b=frame['source_bbox'];own=owner[t:b,l:r]==component
            core=labels[t:b,l:r]==component
            self.assertEqual(int(core.sum()),int(counts[component]))
            for variant,source in [('filled',filled),('empty',empty)]:
                asset=frame['variants'][variant];cropped=arr(asset['path'])
                self.assertEqual(frame['size'],[cropped.shape[1],cropped.shape[0]])
                self.assertTrue(np.array_equal(cropped[own],source[t:b,l:r][own]))
                self.assertTrue(np.array_equal(cropped[core],source[t:b,l:r][core]))
                self.assertTrue(np.all(cropped[~own]==0))
                self.assertEqual(asset['sha256'],digest(asset['path']))
                self.assertEqual(asset['rgba_sha256'],hashlib.sha256(cropped.tobytes()).hexdigest())

    def test_eye_is_aligned_and_masks_gaze(self):
        eye=MAN['eye'];l,t,r,b=eye['source_crop_bbox']
        outline=arr(eye['assets']['outline']['path']);pupil=arr(eye['assets']['pupil']['path'])
        mask=arr(eye['assets']['mask']['path'])[:,:,3]
        self.assertEqual(outline.shape,pupil.shape)
        self.assertEqual(outline.shape[:2],mask.shape)
        self.assertEqual([r-l,b-t],eye['size'])
        for role,name in [('outline','HALLWAY-Frame-Eye-outline.png'),('pupil','HALLWAY-Frame-Eye.png')]:
            source=arr('Assets/Art/Frames/Originals/'+name)
            self.assertTrue(np.array_equal(arr(eye['assets'][role]['path']),source[t:b,l:r]))
        self.assertEqual(635448,int((mask>0).sum()))
        self.assertEqual(1,ndimage.label(mask>0)[1])
        self.assertFalse(np.any((mask>0)&(outline[:,:,3]>0)))
        self.assertTrue(mask[mask.shape[0]//2,mask.shape[1]//2]>0)
        for shift in [-330,0,330]:
            shifted=np.roll(pupil[:,:,3],shift,axis=1)
            clipped=shifted.astype(np.uint16)*mask.astype(np.uint16)//255
            self.assertFalse(np.any(clipped[mask==0]))
            self.assertTrue(np.any(clipped>0))
        self.assertFalse(np.any(pupil[:,:,3][mask==0]))

    def test_import_metadata_preserves_quality(self):
        guids=[];sprite_ids=[]
        for path in (ROOT/'Assets/Art/Frames').rglob('*.png'):
            meta=Path(str(path)+'.meta').read_text()
            guids.append(re.search(r'^guid: ([a-f0-9]{32})$',meta,re.M).group(1))
            sprite_ids.append(re.search(r'^    spriteID: ([a-f0-9]{32})$',meta,re.M).group(1))
            self.assertIn('  spriteMeshType: 1',meta)
            self.assertIn('  spritePixelsToUnits: 100',meta)
            self.assertIn('    enableMipMap: 0',meta)
            self.assertNotIn('    textureCompression: 1',meta)
            self.assertIn('    textureCompression: 0',meta)
        # The original 57 textures are unchanged; the gallery may add explicitly
        # declared opening/backing derivatives without invalidating provenance.
        originals={s['path'] for s in MAN['sources']}
        originals.update(v['path'] for f in MAN['frames'] for v in f['variants'].values())
        originals.update(a['path'] for a in MAN['eye']['assets'].values())
        self.assertEqual(57,len(originals))
        derivatives=set()
        masks_path=ROOT/'Tools/WatchingFrameTests/frame-opening-masks.json'
        if masks_path.is_file():
            for mask in json.loads(masks_path.read_text())['frames']:
                derivatives.update([mask['path'],mask['backing_path']])
        actual={str(p.relative_to(ROOT)) for p in (ROOT/'Assets/Art/Frames').rglob('*.png')}
        self.assertEqual(originals | derivatives,actual)
        self.assertEqual(len(originals)+len(derivatives),len(guids))
        self.assertEqual(len(guids),len(set(guids)))
        self.assertEqual(len(sprite_ids),len(set(sprite_ids)))

if __name__=='__main__': unittest.main(verbosity=2)
