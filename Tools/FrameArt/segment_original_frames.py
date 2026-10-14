#!/usr/bin/env python3
"""Lossless crops of the user's frame sheets; deterministic Unity import metadata.

The PNG crops copy original RGBA frame pixels without resampling, recolouring,
or alpha thresholding. Where frame bounding boxes overlap, ownership is assigned
to the nearest complete frame component and foreign frame pixels are excluded. Four source images are retained byte-for-byte.
Only the eye-interior mask is derived; it fills the closed transparent region
inside the original painted outline. No replacement frame art is generated.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import uuid

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

REPO = Path(__file__).resolve().parents[2]
ART = REPO / 'Assets/Art/Frames'
GUID_NAMESPACE = uuid.UUID('37e22b31-985b-459c-8a1f-1d6e81f2423a')
ORIGINALS = {
    'Frames-Empty-00.png': 'libfile_804b0efec66081918d12a3fac4e914bc',
    'Frames-Filled-00.png': 'libfile_6de59f4b96588191ba813675d8d16ab6',
    'HALLWAY-Frame-Eye-outline.png': 'libfile_5645ab82aca88191b0a0bab505779419',
    'HALLWAY-Frame-Eye.png': 'libfile_f3175e76d9d481919dd3bf94ded767f7',
}
LABELS = [
 'Brown long horizontal', 'Brown tall crest', 'Gold ornate square',
 'Brown upright oval inset', 'Gold diamond', 'Gold small oval crest',
 'Gold brown cut-corner horizontal', 'Gold upright oval', 'Gold large rectangle',
 'Gold tall crest', 'Brown narrow oval', 'Gold scalloped oval',
 'Gold lobed rectangle', 'Brown small horizontal', 'Gold narrow crest horizontal',
 'Brown small rectangle', 'Gold slender crest', 'Gold scroll landscape',
 'Brown pointed oval', 'Gold round', 'Brown small square B', 'Brown square',
 'Brown small square A', 'Brown diamond', 'Gold wide oval',
]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def guid(path):
    meta = Path(str(path)+'.meta')
    if meta.exists():
        found = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(), re.M)
        if found:
            return found.group(1)
    return uuid.uuid5(GUID_NAMESPACE, path.relative_to(REPO).as_posix()).hex

def folder(path):
    path.mkdir(parents=True, exist_ok=True)
    meta = Path(str(path)+'.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: '+guid(path)+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

def sprite_meta(path):
    # Reuse this project's current TextureImporter serialization format.
    template = (REPO/'Assets/Art/Hallway/HALLWAY-Door-full.png.meta').read_text()
    template = re.sub(r'^guid: .*$', 'guid: '+guid(path), template, flags=re.M)
    template = re.sub(r'^    spriteID: .*$', '    spriteID: '+uuid.uuid5(GUID_NAMESPACE, path.relative_to(REPO).as_posix()+'-sprite').hex, template, flags=re.M)
    template = template.replace('  spriteMeshType: 0', '  spriteMeshType: 1')
    template = template.replace('    textureCompression: 1', '    textureCompression: 0')
    Path(str(path)+'.meta').write_text(template)

def largest_region(mask):
    lab, _ = ndimage.label(mask)
    sizes = np.bincount(lab.ravel()); sizes[0] = 0
    assert sizes.max() > 0, 'No enclosed interior detected'
    return lab == int(np.argmax(sizes))

def bbox(mask):
    y,x = np.where(mask)
    return [int(x.min()),int(y.min()),int(x.max()+1),int(y.max()+1)]

def main_components(alpha):
    lab, _ = ndimage.label(alpha>0, structure=np.ones((3,3)))
    sizes=np.bincount(lab.ravel()); boxes=ndimage.find_objects(lab)
    ids=[int(i) for i in np.where(sizes>500)[0] if i]
    return lab, sorted(ids,key=lambda i:(boxes[i-1][0].start,boxes[i-1][1].start)), boxes

def main(source, qa):
    qa.mkdir(parents=True,exist_ok=True)
    for d in [ART,ART/'Originals',ART/'Segmented Frames',ART/'Eye']:
        folder(d)
    manifest={'schema':1,'source_policy':'Original PNG files retained byte-for-byte. Segmented frame PNGs retain exact source RGBA pixels, with four pixels of padding and no resampling or alpha edits to retained art. Overlapping crop rectangles exclude foreign frame pixels using nearest-main-component ownership. Every main frame component is fully retained. Tiny disconnected sheet-edge marks outside all crop rectangles remain in the originals.','coordinate_system':'Source and crop bounding boxes use [left, top, right, bottom], right/bottom exclusive. Interior bboxes are local to the matching cropped sprite.','sprite_import':{'pixels_per_unit':100,'pivot':[0.5,0.5],'mesh':'FullRect','filter':'Bilinear','compression':'None','mipmaps':False},'sources':[],'frames':[]}
    for name,libid in ORIGINALS.items():
        src=source/name; dest=ART/'Originals'/name
        if src.resolve()!=dest.resolve():
            shutil.copy2(src,dest)
        sprite_meta(dest)
        assert sha(src)==sha(dest)
        im=Image.open(dest)
        manifest['sources'].append({'filename':name,'library_file_id':libid,'path':dest.relative_to(REPO).as_posix(),'size':list(im.size),'sha256':sha(dest),'guid':guid(dest),'original_byte_check':True})
    filled=np.array(Image.open(source/'Frames-Filled-00.png').convert('RGBA'))
    empty=np.array(Image.open(source/'Frames-Empty-00.png').convert('RGBA'))
    assert filled.shape==empty.shape==(857,2048,4)
    lab,ids,boxes=main_components(filled[:,:,3]); assert len(ids)==25
    major=np.isin(lab,ids)
    nearest=ndimage.distance_transform_edt(~major,return_distances=False,return_indices=True)
    owner=lab[nearest[0],nearest[1]]
    eye_a=np.array(Image.open(source/'HALLWAY-Frame-Eye-outline.png').convert('RGBA'))
    eye_inside=largest_region(ndimage.binary_fill_holes(eye_a[:,:,3]>0)&(eye_a[:,:,3]==0))
    eye_box=[206,431,1860,1284]
    l,t,r,b=eye_box
    cropped_inside=eye_inside[t:b,l:r]
    mask_pixels=np.zeros((b-t,r-l,4),dtype=np.uint8)
    mask_pixels[:,:,:3]=255;mask_pixels[:,:,3]=cropped_inside.astype(np.uint8)*255
    mask_path=ART/'Eye/HALLWAY-Frame-Eye-interior-mask.png'
    Image.fromarray(mask_pixels).save(mask_path);sprite_meta(mask_path)
    eye={'source_crop_bbox':eye_box,'size':[r-l,b-t],'pivot':[0.5,0.5],
         'outline_alpha_bbox_source':bbox(eye_a[:,:,3]>0),
         'interior_bbox_source':bbox(eye_inside),
         'interior_bbox_local':bbox(cropped_inside),
         'interior_pixel_count':int(eye_inside.sum()),
         'mask_method':'Exact enclosed fully-transparent interior of original outline, found by flood fill, largest connected enclosed region. Stroke itself is excluded. Original outline is drawn over the mask edge.',
         'assets':{}}
    eye['assets']['mask']={'path':mask_path.relative_to(REPO).as_posix(),'guid':guid(mask_path),'sha256':sha(mask_path)}
    for label,name in [('outline','HALLWAY-Frame-Eye-outline.png'),('pupil','HALLWAY-Frame-Eye.png')]:
        im=Image.open(source/name).convert('RGBA');path=ART/'Eye'/name
        crop=im.crop(eye_box);crop.save(path);sprite_meta(path)
        assert np.array_equal(np.array(crop),np.array(im)[t:b,l:r])
        eye['assets'][label]={'path':path.relative_to(REPO).as_posix(),'guid':guid(path),'sha256':sha(path),'source_pixel_check':True,'alpha_bbox_source':bbox(np.array(im)[:,:,3]>0),'alpha_bbox_local':bbox(np.array(crop)[:,:,3]>0)}
    manifest['eye']=eye
    for j,idx in enumerate(ids,1):
        sy,sx=boxes[idx-1];box=[max(0,sx.start-4),max(0,sy.start-4),min(2048,sx.stop+4),min(857,sy.stop+4)]
        l,t,r,b=box
        other_major=[int(k) for k in np.unique(lab[t:b,l:r]) if k in ids and k!=idx]
        own=owner[t:b,l:r]==idx
        assert np.all(own[lab[t:b,l:r]==idx]), 'Main component would be clipped'
        empty_crop=empty[t:b,l:r].copy();empty_crop[~own]=0
        interior=largest_region(ndimage.binary_fill_holes(empty_crop[:,:,3]>0)&(empty_crop[:,:,3]==0))
        ib=bbox(interior); yy,xx=np.where(interior)
        item={'id':j,'name':LABELS[j-1],'source_bbox':box,'main_component_bbox':[sx.start,sy.start,sx.stop,sy.stop],
              'size':[r-l,b-t],'interior_bbox_local':ib,'interior_centroid_local':[float(xx.mean()),float(yy.mean())],
              'interior_bbox_normalized':[ib[0]/(r-l),ib[1]/(b-t),ib[2]/(r-l),ib[3]/(b-t)],
              'interior_pixels':int(interior.sum()),'overlapping_foreign_components_excluded':other_major,'variants':{}}
        for variant,arr in [('Filled',filled),('Empty',empty)]:
            dest=ART/'Segmented Frames'/f'Frame-{variant}-{j:02d}.png'
            source_crop=arr[t:b,l:r]
            crop=source_crop.copy();crop[~own]=0
            Image.fromarray(crop).save(dest);sprite_meta(dest)
            loaded=np.array(Image.open(dest))
            assert np.array_equal(loaded[own],source_crop[own])
            assert np.all(loaded[~own]==0)
            assert np.array_equal(loaded[lab[t:b,l:r]==idx],source_crop[lab[t:b,l:r]==idx])
            item['variants'][variant.lower()]={'path':dest.relative_to(REPO).as_posix(),'guid':guid(dest),'sha256':sha(dest),'rgba_sha256':hashlib.sha256(crop.tobytes()).hexdigest(),'source_pixel_check':True,'nonzero_alpha_pixels':int((crop[:,:,3]>0).sum()),'excluded_foreign_alpha_pixels':int(((source_crop[:,:,3]>0)&~own).sum()),'complete_main_component_retained':True}
        manifest['frames'].append(item)
    manifest['validation']={'paired_frames':25,'frame_crops':50,'original_byte_checks_passed':4,'frame_source_pixel_checks_passed':50,'eye_source_pixel_checks_passed':2,'foreign_frame_components_in_crops':0,'retained_source_pixel_mutations':0,'all_main_frame_components_fully_retained':True,'eye_mask_connected_components':int(ndimage.label(cropped_inside)[1]),'mask_outline_overlap_pixels':int(np.logical_and(cropped_inside,eye_a[eye_box[1]:eye_box[3],eye_box[0]:eye_box[2],3]>0).sum())}
    manifest_path=REPO/'Tools/FrameArt/frame-segmentation.json'
    manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
    # Contact sheet is a review artifact only; no preview resizing enters game assets.
    cellw,cellh=440,230
    contact=Image.new('RGB',(5*cellw,5*cellh+230),(29,35,42));d=ImageDraw.Draw(contact)
    font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',15)
    for item in manifest['frames']:
        j=item['id']-1;cx=(j%5)*cellw;cy=(j//5)*cellh
        d.rectangle([cx+3,cy+3,cx+cellw-3,cy+cellh-3],outline=(78,90,101))
        d.text((cx+10,cy+9),f"{item['id']:02d}  {item['name']}",font=font,fill=(241,231,209))
        for k,variant in enumerate(['empty','filled']):
            im=Image.open(REPO/item['variants'][variant]['path']);im.thumbnail((196,174),Image.Resampling.LANCZOS)
            px=cx+k*215+(215-im.width)//2;py=cy+38+(172-im.height)//2
            contact.paste(im,(px,py),im)
            d.text((cx+k*215+68,cy+208),variant,font=font,fill=(182,197,202))
    y=5*cellh+12;d.text((15,y),'Original eye, masked pupil at center and looking sideways. Assets retain the same cropped UV canvas.',font=font,fill=(241,231,209))
    outline=Image.open(REPO/eye['assets']['outline']['path']);pupil=Image.open(REPO/eye['assets']['pupil']['path'])
    for k,shift in enumerate([0,-330,330]):
        layer=Image.new('RGBA',outline.size);layer.alpha_composite(pupil,(shift,0));pa=np.array(layer);pa[:,:,3]=(pa[:,:,3].astype(np.float32)*(mask_pixels[:,:,3]/255)).astype(np.uint8)
        out=Image.fromarray(pa);out.alpha_composite(outline);out.thumbnail((550,165),Image.Resampling.LANCZOS)
        contact.paste(out,(30+k*700,y+30),out)
    contact.save(qa/'frame-pairs-and-eye-contact-sheet.png')
    print(json.dumps({'manifest':str(manifest_path),'qa_contact_sheet':str(qa/'frame-pairs-and-eye-contact-sheet.png'),'validation':manifest['validation'],'eye':eye},indent=2))

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--qa',type=Path,required=True);args=ap.parse_args();main(args.source,args.qa)
