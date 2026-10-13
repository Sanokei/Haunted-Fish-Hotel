#!/usr/bin/env python3
"""Validate saved Unity YAML and optionally render a labeled source-layout proof.
Pillow composition is not a Unity capture and does not verify the GPU blur shader.
"""
import argparse, copy, json, math, re
from pathlib import Path
import yaml

ROOT=Path(__file__).resolve().parents[2]
PREFAB=ROOT/'Assets/Resources/AtticRoundCutscene.prefab'

def load(path):
    matches=list(re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',path.read_text(),re.M|re.S))
    result={int(m[2]):next(iter(yaml.safe_load(m[3]).values())) for m in matches}
    assert len(result)==len(matches), 'Duplicate serialized file IDs'
    return result

def verify():
    data=load(PREFAB); checks=0
    def check(value,message):
        nonlocal checks
        assert value,message
        checks+=1
    def visit(value):
        if isinstance(value,dict):
            if 'fileID' in value and value['fileID'] and 'guid' not in value:
                check(value['fileID'] in data,'Dangling local reference '+str(value))
            for v in value.values():visit(v)
        elif isinstance(value,list):
            for v in value:visit(v)
    visit(list(data.values()))
    for id,node in data.items():
        if 'm_Children' in node:
            for child in node['m_Children']:
                check(data[child['fileID']]['m_Father']['fileID']==id,'Nonreciprocal authored hierarchy')
    ui=data[5003]; rig=ui['_ParallaxRigs'][0]; poses={p['Tag']:p for p in rig['Poses']}
    check(rig['Tag']=='hotel_camera' and rig['Layers']==[{'Target':{'fileID':121},'Depth':1}], 'One saved UI strip owns the camera motion')
    check(poses['hallway_start']['Camera']=={'x':0,'y':0}, 'Start pose returns to unchanged four-fish framing')
    check(poses['hallway_end']['Camera']['x']>6000 and poses['hallway_end']['Camera']['y']==0,'End-to-start travel is horizontal')
    check(all(p['Push']==0 for p in poses.values()), 'Camera poses never rescale hallway/fish')
    assets=data[5006]
    check(len(assets['IntroFish'])==4 and assets['IntroFishWorldX']==[-10.5,-8.5,-6.5,-4.5], 'Four saved fish retain starting positions')
    check(assets['IntroFishHeightFraction']==.62, 'Existing fish scale retained')
    door=data[92000023];rect=data[92000021]
    check(door['m_Sprite']['guid']=='5a0322f79d9b2064c89dbd1823a3be41','Requested open-attic-door art is bound')
    check(abs(rect['m_SizeDelta']['x']/rect['m_SizeDelta']['y']-1172/1054)<.00001,'Door preserves actual source aspect ratio')
    scale=data[131]['m_SizeDelta']['x']/68
    check(abs((rect['m_AnchoredPosition']['x']+7*rect['m_SizeDelta']['y']/1054)/scale+19-51.8)<.00001,'Door opaque center matches actual Game end X51.8')
    check(abs(rect['m_AnchoredPosition']['y']-rect['m_SizeDelta']['y']/2+236)<.00001,'Door rests on the same authored hallway floor as the fish')
    graphics={i for i,n in data.items() if n.get('m_Script',{}).get('guid')=='fe87c0e1cc204ed48ad3b37840f39efc'}
    expected=graphics-{113,92000013}-set(range(7103,7274,10))
    for id in (5004,92000004):
        refs=[r['fileID'] for r in data[id]['_Graphics']]
        check(len(set(refs))==len(refs),'Blur targets are unique')
        check(set(refs)==expected,'Blur covers actual hallway/decor/fish graphics, not only empty parent Images')
    for id in (590,7000,92000000,92000010):check(not data[id]['m_IsActive'],'Cinematic effects start disabled')
    check(ui['_WhiteFrame']=={'fileID':92000010},'Flash API uses saved overlay')
    check(data[101]['m_Children'][-1]=={'fileID':92000011},'Flash renders above the cinematic')
    mats=[load(ROOT/'Assets/Resources'/name)[2100000] for name in ('AtticReturnBlur.mat','HallwayPanBlur.mat')]
    axes=[next(p['_Direction'] for p in m['m_SavedProperties']['m_Colors'] if '_Direction' in p) for m in mats]
    check(axes==[{'r':0,'g':1,'b':0,'a':0},{'r':1,'g':0,'b':0,'a':0}],'Separate authored vertical and horizontal blur directions')
    shader=(ROOT/'Assets/Resources/AtticReturnBlur.shader').read_text()
    check('ddx(i.uv)' in shader and 'ddy(i.uv)' in shader and 'o.uv = v.uv;' in shader and 'v.vertex.xy +=' not in shader,'Screen-axis blur does not rescale geometry or shrink adjacent panels')
    print(f'PASS {checks} serialized cinematic reference, geometry, art, staging, scale and effect-scope checks.')
    return data,checks

def render(data,out):
    from PIL import Image,ImageDraw,ImageFont,ImageChops
    out.mkdir(parents=True,exist_ok=True)
    index={}
    for meta in (ROOT/'Assets').rglob('*.png.meta'):
        m=re.search(r'^guid: (\w+)',meta.read_text(),re.M)
        if m:index[m[1]]=Path(str(meta)[:-5])
    cache={}
    def sprite(guid):
        if guid not in cache:cache[guid]=Image.open(index[guid]).convert('RGBA')
        return cache[guid].copy()
    font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',18)
    def scene(camera):
        canvas=Image.new('RGBA',(1280,720),(16,11,42,255))
        def draw_node(id,parent=(0,0)):
            rect=data[id];go=data[rect['m_GameObject']['fileID']]
            if not go['m_IsActive']:return
            pos=rect['m_AnchoredPosition']; x=parent[0]+pos['x']; y=parent[1]+pos['y']
            if id==121:x-=camera
            for comp in go['m_Component']:
                graphic=data[comp['component']['fileID']]
                if graphic.get('m_Script',{}).get('guid')!='fe87c0e1cc204ed48ad3b37840f39efc':continue
                color=graphic['m_Color']
                if not color['a']:continue
                w=rect['m_SizeDelta']['x'];h=rect['m_SizeDelta']['y'];pivot=rect['m_Pivot']
                left=640+x-w*pivot['x'];top=360-y-h*(1-pivot['y'])
                if left>1280 or left+w<0 or top>720 or top+h<0:continue
                guid=graphic.get('m_Sprite',{}).get('guid')
                if guid:
                    img=sprite(guid)
                    if graphic['m_PreserveAspect']:
                        ratio=min(w/img.width,h/img.height);ww=img.width*ratio;hh=img.height*ratio
                        left+=(w-ww)/2;top+=(h-hh)/2;w,h=ww,hh
                    img=img.resize((max(1,round(w)),max(1,round(h))),Image.Resampling.LANCZOS)
                    if color['a']!=1:img.putalpha(img.getchannel('A').point(lambda a:round(a*color['a'])))
                else:
                    # Flat authored beam, never resize a giant backing texture.
                    a=max(0,round(left));b=max(0,round(top));c=min(1280,round(left+w));d=min(720,round(top+h))
                    if c>a and d>b:ImageDraw.Draw(canvas).rectangle((a,b,c,d),fill=tuple(round(color[k]*255) for k in ('r','g','b','a')))
                    continue
                canvas.alpha_composite(img,(round(left),round(top)))
            for child in rect['m_Children']:draw_node(child['fileID'],(x,y))
        # Render the existing authored hotel tree in its sibling order.
        draw_node(121)
        # Appearance is a representative source-prefab fish, staged with unchanged production math.
        # Network-specific colors and live fin/lean animation are intentionally not asserted here.
        pixels=100; body=sprite('731a8404c59e9304eab0d35f104ffe90');fin=sprite('431ebc1345eae394cbd460c823544adf')
        # Tail importer pivot (0.46728516, 0.26757812) exactly compensates
        # the saved fin pivot offset (-0.67, -4.76), so their canvases align.
        ensemble=Image.new('RGBA',body.size)
        ensemble.alpha_composite(fin,(0,0));ensemble.alpha_composite(body,(0,0))
        height=486*.62;width=height
        ensemble=ensemble.resize((round(width),round(height)),Image.Resampling.LANCZOS)
        stripX=data[121]['m_AnchoredPosition']['x']-camera;scale=data[131]['m_SizeDelta']['x']/68
        for world in data[5006]['IntroFishWorldX']:
            x=640+stripX+(world-19)*scale;bottom=360+236
            canvas.alpha_composite(ensemble,(round(x-width/2),round(bottom-height)))
        return canvas.convert('RGB')
    frames=[('01-open-door-end',6666.666667,'OPEN ATTIC DOOR / end of hallway'),('02-pan-midpoint',3333.333333,'HORIZONTAL PAN / midpoint, blur shader not simulated'),('03-four-fish-start',0,'RETURN TO START / same four authored fish slots')]
    thumbs=[]
    for name,camera,label in frames:
        image=scene(camera);proof=Image.new('RGB',(1280,786),(16,11,42));proof.paste(image,(0,66))
        draw=ImageDraw.Draw(proof);draw.text((20,10),label,font=font,fill=(255,230,169));draw.text((20,35),'SOURCE-LAYOUT COMPOSITE • actual saved art/poses • NOT a Unity runtime capture',font=font,fill=(173,202,154))
        proof.save(out/(name+'.png'));thumbs.append(proof)
    sheet=Image.new('RGB',(1280,786*3));
    for i,img in enumerate(thumbs):sheet.paste(img,(0,i*786))
    sheet.save(out/'hallway-pan-source-proof.png')
    (out/'verification.json').write_text(json.dumps({'source':str(PREFAB.relative_to(ROOT)),'status':'Authored-source proof only. Native Unity execution and shader render remain unverified.','fish':'Representative player-prefab body/fin art, unchanged four slots and height math. Network-specific appearance is not simulated.','poses':['open attic door','horizontal midpoint','four-fish start']},indent=2))
    print('Wrote labeled source-layout proof to '+str(out))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--proof-dir',type=Path);args=parser.parse_args()
    data,count=verify()
    if args.proof_dir:render(data,args.proof_dir)
