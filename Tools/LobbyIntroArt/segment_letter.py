#!/usr/bin/env python3
"""Lossless source segmentation explicitly approved by the user.

Only crop and exterior alpha change. No RGB channel is changed, interpolated,
retyped or redrawn. This offline authoring tool never runs in the game.
Requires Pillow, NumPy and SciPy.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import shutil

import numpy as np
from PIL import Image
from scipy.ndimage import binary_closing, binary_fill_holes

from author_lobby_intro import ROOT, ART, guid

SOURCE_HASH = "15fd9ec044df3b738f6e9ff4a0d6c8305cc0ef034a50cd7de3c43617ae45955c"
# Pixel coordinates from the 2048 x 1271 original, top-left origin, right/bottom exclusive.
BOUNDS = {"letter": (382, 189, 883, 516), "letter_read": (1213, 146, 1730, 892)}


def write_meta(path):
    template=(ROOT / "Assets/Art/Hallway/HALLWAY-Door-full.png.meta").read_text()
    relative=path.relative_to(ROOT)
    template=re.sub(r"guid: \w+",f"guid: {guid(relative)}",template,count=1)
    template=re.sub(r"spriteID: \w+",f"spriteID: {guid(str(relative)+':sprite')}",template)
    template=template.replace("spriteGenerateFallbackPhysicsShape: 1","spriteGenerateFallbackPhysicsShape: 0")
    template=template.replace("textureCompression: 1","textureCompression: 0")
    path.with_name(path.name+".meta").write_text("\n".join(line.rstrip() for line in template.splitlines())+"\n")


def segment(source_path=None):
    source=ART / "Letter-source.png"
    candidate=Path(source_path) if source_path is not None else source
    digest=hashlib.sha256(candidate.read_bytes()).hexdigest()
    if digest!=SOURCE_HASH:
        raise ValueError("Source differs from the inspected and approved attachment; inspect new boundaries first.")
    if candidate.resolve()!=source.resolve():
        shutil.copyfile(candidate,source)
    write_meta(source)
    with Image.open(source) as image:
        original=np.asarray(image.convert("RGBA"))
    assert original.shape==(1271,2048,4)
    manifest={"source":"Assets/Art/LobbyIntro/Letter-source.png","source_sha256":digest,
              "source_size":[2048,1271],"coordinate_system":"top-left origin; right/bottom exclusive",
              "method":"Lossless crop; original RGB copied byte for byte; exterior-only binary alpha mask.",
              "mask":"All nonwhite source pixels protected. A one-pixel four-neighbor closing seals tiny outline gaps for interior filling only. Original ink mask is unioned back. Enclosed paper stays opaque.",
              "imagegen":"Two extraction trials were rejected for resampling/redrawing and are not used in the project.",
              "assets":{}}
    for name,(left,top,right,bottom) in BOUNDS.items():
        crop=original[top:bottom,left:right].copy()
        ink=np.any(crop[:,:,:3]!=255,axis=2)
        # A tiny drawing-border gap must not make the white paper transparent.
        # Closing affects only this temporary binary barrier, never source RGB.
        mask=binary_fill_holes(binary_closing(ink,iterations=1)) | ink
        crop[:,:,3]=np.where(mask,255,0)
        assert np.array_equal(crop[:,:,:3],original[top:bottom,left:right,:3])
        assert np.all(crop[:,:,3][ink]==255)
        assert mask[mask.shape[0]//2,mask.shape[1]//2]
        assert not (mask[0,:].any() or mask[-1,:].any() or mask[:,0].any() or mask[:,-1].any())
        dest=ART / (name+".png")
        Image.fromarray(crop).save(dest,optimize=True)
        write_meta(dest)
        y,x=np.nonzero(ink)
        manifest["assets"][name]={
            "path":str(dest.relative_to(ROOT)),"crop_box":[left,top,right,bottom],
            "size":[right-left,bottom-top],"source_ink_bounds":[int(x.min()+left),int(y.min()+top),int(x.max()+left+1),int(y.max()+top+1)],
            "protected_drawn_pixels":int(ink.sum()),"opaque_pixels":int(mask.sum()),
            "transparent_pixels":int(mask.size-mask.sum()),"rgb_bytes_changed":0,
            "drawn_pixels_removed":0,"sha256":hashlib.sha256(dest.read_bytes()).hexdigest()}
    provenance=ROOT / "Tools/LobbyIntroArt/letter_asset_provenance.json"
    provenance.write_text(json.dumps(manifest,indent=2)+"\n")
    return manifest


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source",type=Path,help="Original approved IMG_0101.PNG; defaults to archived source.")
    print(json.dumps(segment(parser.parse_args().source),indent=2))
