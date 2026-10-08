import pathlib,re,uuid,json,math
root=pathlib.Path.cwd()
art=root/'Assets/Art/BossFight'
def guid(path):
 m=path.with_name(path.name+'.meta')
 if m.exists():return re.search(r'guid: (\w+)',m.read_text()).group(1)
 g=uuid.uuid4().hex;m.write_text('fileFormatVersion: 2\nguid: '+g+'\n',encoding='utf-8');return g
for p in (root/'Assets/Scripts/BossFight').glob('*.cs'):guid(p)
for p in [root/'Assets/Scripts/BossFight',art,root/'Assets/Resources/BossFight']:guid(p)
sg={p.stem:guid(p) for p in (root/'Assets/Scripts/BossFight').glob('*.cs')}
matbase=(root/'Assets/Art/wood.mat').read_text(encoding='utf-8')
colors={'Felt':(.035,.21,.15,1),'Walnut':(.18,.075,.04,1),'Brass':(.75,.48,.17,1),'Ghost':(.76,.24,.35,1),'Fish':(.25,.68,.72,1),'CueBall':(.94,.93,.82,1),'Floor':(.095,.09,.12,1),'Walls':(.20,.12,.17,1),'Lines':(.45,.63,.51,1)}
mg={}
for n,c in colors.items():
 p=art/(n+'.mat');s=matbase.replace('m_Name: wood','m_Name: '+n)
 s=s.replace('m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}', 'm_Shader: {fileID: 4800000, guid: 650dd9526735d5b46b79224bc6e94025, type: 3}')
 s=s.replace('    m_Colors:', '    m_Colors:\n    - _BaseColor: {r: %s, g: %s, b: %s, a: %s}'%c)
 s=re.sub(r'(- _Color: )\{[^}]+\}',lambda m:m[1]+'{r: %s, g: %s, b: %s, a: %s}'%c,s)
 s=re.sub(r'(- _Glossiness: )[\d.]+',r'\g<1>0.15' if n=='Felt' else r'\g<1>0.3',s)
 # Remove borrowed wood textures; these are deliberate solid table materials.
 s=re.sub(r'm_Texture: \{fileID: [^}]+\}', 'm_Texture: {fileID: 0}',s)
 p.write_text(s,encoding='utf-8');mg[n]=guid(p)
scoremat=art/'ScoreText.mat'
scoremat.write_text((art/'CueBall.mat').read_text(encoding='utf-8').replace('m_Name: CueBall','m_Name: ScoreText').replace('m_Shader: {fileID: 4800000, guid: 650dd9526735d5b46b79224bc6e94025, type: 3}', 'm_Shader: {fileID: 4800000, guid: '+guid(art/'ScoreText.shader')+', type: 3}'),encoding='utf-8')
# Use the actually installed shader identity rather than assuming package hashes.
sm=scoremat.read_text(encoding='utf-8')
sm=re.sub(r'm_Shader: \{[^}]+\}', 'm_Shader: {fileID: 4800000, guid: '+guid(art/'ScoreText.shader')+', type: 3}',sm)
scoremat.write_text(sm,encoding='utf-8');mg['ScoreText']=guid(scoremat)
physics=art/'AirHockey.physicsMaterial'
physics.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!134 &13400000\nPhysicsMaterial:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: AirHockey\n  dynamicFriction: 0\n  staticFriction: 0\n  bounciness: 0.98\n  frictionCombine: 1\n  bounceCombine: 3\n',encoding='utf-8')
phys=guid(physics)
src=(root/'Assets/Scenes/Game.unity').read_text(encoding='utf-8')
blocks=re.split(r'(?=--- !u!)',src)
templates={t:next(b for b in blocks if b.startswith('--- !u!'+str(t)+' ')) for t in [23,33,65,20]}
head=''.join(b for b in blocks if not b.startswith('--- !u!') or re.match(r'--- !u!(29|104|157|196) ',b))
# Default ambient mode makes the standalone scene readable without consuming hallway lights.
head=head.replace('m_AmbientMode: 0','m_AmbientMode: 3').replace('m_AmbientSkyColor: {r: 0.212, g: 0.227, b: 0.259, a: 1}','m_AmbientSkyColor: {r: 0.75, g: 0.75, b: 0.75, a: 1}')
out=[];counter=600000;roots=[];children={};nodes={}
def common(go):return '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: '+str(go)+'}\n'
def vec(v,keys='xyz'):return '{'+', '.join(k+': '+str(x) for k,x in zip(keys,v))+'}'
def node(name,pos=(0,0,0),scale=(1,1,1),parent=0,active=1,rot=(0,0,0,1)):
 global counter
 counter+=10;go=counter;tr=go+1
 nodes[go]=[name,pos,scale,parent,active,rot,[tr]];children[tr]=[]
 if parent:children[parent].append(tr)
 else:roots.append(tr)
 counter+=2
 return go,tr
def comp(go,t,body):
 global counter
 counter+=1;id=counter;nodes[go][6].append(id);out.append('--- !u!'+str(t)+' &'+str(id)+'\n'+body);return id
def clone(go,t,mods={}):
 s=templates[t];s=re.sub(r'^--- !u!\d+ &\d+\n','',s);s=re.sub(r'm_GameObject: \{fileID: \d+\}', 'm_GameObject: {fileID: '+str(go)+'}',s)
 for k,v in mods.items():s=re.sub(r'(?m)^  '+re.escape(k)+r': .*$', '  '+k+': '+str(v),s)
 if t==65:s=s.replace('m_Material: {fileID: 0}','m_Material: {fileID: 13400000, guid: '+phys+', type: 2}')
 return comp(go,t,s)
def mono(go,g,fields):
 return comp(go,114,'MonoBehaviour:\n'+common(go)+'  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+g+', type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n'+fields)
def cube(name,pos,scale,material,parent=0,collide=True):
 go,tr=node(name,pos,scale,parent)
 clone(go,33)
 clone(go,23,{'m_CastShadows':0,'m_ReceiveShadows':0}) 
 # Renderer template has one material; replace exact borrowed material in last block.
 out[-1]=re.sub(r'guid: a6f8bfa44d3d6fb4fad3104d502c7404', 'guid: '+mg[material],out[-1])
 if collide:clone(go,65)
 return go,tr
def body(go,kin=True):
 return comp(go,54,'Rigidbody:\n'+common(go)+'  serializedVersion: 5\n  m_Mass: 1\n  m_LinearDamping: 0.05\n  m_AngularDamping: 0.05\n  m_CenterOfMass: {x: 0, y: 0, z: 0}\n  m_InertiaTensor: {x: 1, y: 1, z: 1}\n  m_InertiaRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_IncludeLayers: {serializedVersion: 2, m_Bits: 0}\n  m_ExcludeLayers: {serializedVersion: 2, m_Bits: 0}\n  m_ImplicitCom: 1\n  m_ImplicitTensor: 1\n  m_UseGravity: 0\n  m_IsKinematic: '+str(int(kin))+'\n  m_Interpolate: 1\n  m_Constraints: 82\n  m_CollisionDetection: 1\n')
def label(name,color):
 go,tr=node(name);mono(go,'bcdf6c616326c4aaeb610b335fbb67fe','  useDefaultBackgroundColor: 0\n  backgroundColor: {serializedVersion: 2, rgba: '+str(color)+'}\n  useDefaultTextColor: 0\n  textColor: {serializedVersion: 2, rgba: 4294967295}\n  font: {fileID: 0}\n  fontSize: 12\n  fontStyle: 1\n  alignment: 4\n  textDropShadow: 0\n');return tr
arena=label('── BOSS FIGHT / ARENA ──',4281224143)
tablego,table=node('Felt hockey table',(1000,0,0),parent=arena)
tableparts=[]
def part(n,p,s,m,col=True):
 tableparts.append((n,p,s,m));return cube(n,p,s,m,table,col)
part('Felt playing surface',(0,1.5,0),(6,.2,10),'Felt')
part('Walnut cabinet',(0,1.15,0),(6.55,.5,10.55),'Walnut')
for x in [-3.2,3.2]:part('Long cushioned rail',(x,1.85,0),(.4,.6,10.5),'Walnut')
# Split end rails leave actual open goals, not painted pockets.
for z in [-5.2,5.2]:
 for x in [-2.1,2.1]:part('Split goal rail',(x,1.85,z),(2.2,.6,.4),'Walnut')
 for x in [-1.1,1.1]:part('Brass goal post',(x,1.85,z),(.1,.55,.2),'Brass')
 part('Goal tray',(0,1.45,z+(.45 if z>0 else -.45)),(2.2,.1,.8),'Brass')
for x in [-2.4,2.4]:
 for z in [-4.2,4.2]:part('Tapered table leg',(x,.55,z),(.45,1.1,.45),'Walnut')
part('Centre stripe',(0,1.606,0),(5.98,.012,.05),'Lines',False)
room=label('── ROOM / OUTWARD WALLS ──',4282331233)
cube('Floor',(1000,-.2,0),(16,.4,20),'Floor',room)
for x in [991.75,1008.25]:cube('Outside side wall',(x,2.5,0),(.5,5,20.5),'Walls',room)
for z in [-10.25,10.25]:cube('Outside end wall',(1000,2.5,z),(16,.5,1),'Walls',room) # replace below proper thickness
for go in nodes:
 if nodes[go][0]=='Outside end wall':nodes[go][2]=(16,5,.5)
managers=label('── MATCH / MANAGERS ──',4281224143)
managergo,managertr=node('Boss Arena Manager',parent=managers)
mgocomp=counter+1
# Allocate manager before goal references.
managerid=mono(managergo,sg['BossArenaManager'],'')
paddles=[]
for name,z,mat in [('Fish paddle',-3.6,'Fish'),('Ghost paddle',3.6,'Ghost')]:
 go,tr=node(name,(1000,1.7,z),parent=arena)
 visual,visualtr=cube('Paddle cap',(0,0,0),(.9,.125,.9),mat,tr,False)
 out[-2]=out[-2].replace('fileID: 10202','fileID: 10206')
 clone(go,65,{'m_Size':'{x: 0.9, y: 0.25, z: 0.9}'})
 rb=body(go);paddles.append(rb)
 angle=math.radians(70)/2
 iconScale=.4 if mat=='Ghost' else .055
 icon,icontr=node(name+' character badge',(0,.6,0),(iconScale,iconScale,iconScale),tr,rot=(math.sin(angle),0,0,math.cos(angle)))
 sprite=re.search(r'guid: (\w+)',(root/('Assets/Art/ghost.png.meta' if mat=='Ghost' else 'Assets/Art/HotelFish/FISH-right.png.meta')).read_text()).group(1)
 lob=(root/'Assets/Scenes/Lobby.unity').read_text(encoding='utf-8')
 sp=next(b for b in re.split(r'(?=--- !u!)',lob) if b.startswith('--- !u!212 '))
 sp=re.sub(r'^--- !u!212 &\d+\n','',sp)
 sp=re.sub(r'm_GameObject: \{fileID: \d+\}', 'm_GameObject: {fileID: '+str(icon)+'}',sp)
 sp=re.sub(r'm_Sprite: \{[^}]+\}', 'm_Sprite: {fileID: 21300000, guid: '+sprite+', type: 3}',sp)
 sp=re.sub(r'm_SortingOrder: 0','m_SortingOrder: 5',sp)
 comp(icon,212,sp)
go,tr=node('Cue ball',(1000,1.8,0),(.36,.36,.36),arena)
clone(go,33,{'m_Mesh':'{fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}'})
clone(go,23,{'m_CastShadows':0,'m_ReceiveShadows':0});out[-1]=out[-1].replace('a6f8bfa44d3d6fb4fad3104d502c7404',mg['CueBall'])
comp(go,135,'SphereCollider:\n'+common(go)+'  m_Material: {fileID: 13400000, guid: '+phys+', type: 2}\n  m_IsTrigger: 0\n  m_Enabled: 1\n  serializedVersion: 3\n  m_Radius: 0.5\n  m_Center: {x: 0, y: 0, z: 0}\n')
puck=body(go)
for z,fish in [(5.55,1),(-5.55,0)]:
 go,tr=node('Ghost goal' if fish else 'Fish goal',(1000,1.8,z),(2.15,1,.8),arena)
 clone(go,65,{'m_IsTrigger':1})
 mono(go,sg['BossGoal'],'  _Arena: {fileID: '+str(managerid)+'}\n  _FishScores: '+str(fish)+'\n')
_,fishspawn=node('Fish participant spawn',(1000,.1,-7),parent=arena)
_,ghostspawn=node('Ghost participant spawn',(1000,2,7),parent=arena)
views=label('── OWNED VIEW / PRESENTATION ──',4281224143)
# Oblique camera 70 degrees down, looking toward positive Z.
ang=math.radians(70)/2
go,tr=node('Boss participant camera',(1000,13,-4),parent=views,active=0,rot=(math.sin(ang),0,0,math.cos(ang)))
cam=clone(go,20,{'orthographic size':7,'m_Depth':5,'near clip plane':.1,'far clip plane':100,'m_BackGroundColor':'{r: 0.04, g: 0.025, b: 0.045, a: 1}'})
mono(go,'98f0e0687fca42a0b0eb03d19e045cf3','  _Output: {fileID: '+str(cam)+'}\n')
# Three-dimensional authored scoreboard, faces camera.
go,tr=node('Score / first to one versus two',(1000,2,6.9),(.08,.08,.08),views,rot=(math.sin(ang),0,0,math.cos(ang)))
score=comp(go,102,'TextMesh:\n'+common(go)+'  serializedVersion: 3\n  m_Text: FISH 0 / 1     GHOST 0 / 2\n  m_OffsetZ: 0\n  m_CharacterSize: 1\n  m_LineSpacing: 1\n  m_Anchor: 4\n  m_Alignment: 1\n  m_TabSize: 4\n  m_FontSize: 48\n  m_FontStyle: 1\n  m_RichText: 0\n  m_Font: {fileID: 12800000, guid: 0000000000000000e000000000000000, type: 0}\n  m_Color: {r: 0.9, g: 0.85, b: 0.68, a: 1}\n')
scoreRenderer=clone(go,23,{'m_CastShadows':0,'m_ReceiveShadows':0});out[-1]=out[-1].replace('guid: a6f8bfa44d3d6fb4fad3104d502c7404','guid: '+mg['ScoreText'])
mono(go,sg['BossScorePresentation'],'  _Text: {fileID: '+str(score)+'}\n  _Renderer: {fileID: '+str(scoreRenderer)+'}\n')
fields=''.join('  '+k+': {fileID: '+str(v)+'}\n' for k,v in {'_Puck':puck,'_FishPaddle':paddles[0],'_GhostPaddle':paddles[1],'_FishSpawn':fishspawn,'_GhostSpawn':ghostspawn,'_Camera':cam,'_Score':score}.items())
fields+='  _Centre: {x: 1000, y: 1.6, z: 0}\n  _HalfSize: {x: 3, y: 5}\n  _PaddleSpeed: 7\n  _PuckSpeedLimit: 18\n  _ServeSpeed: 4\n  _ServeDelay: 1\n  _SnapshotInterval: 0.05\n  _InputTimeout: 0.3\n  _PaddleRadius: 0.48\n'
out=[b+fields if b.startswith('--- !u!114 &'+str(managerid)+'\n') else b for b in out]
def serialize_nodes(selected=None,shift=0):
 result=[]
 for go,(name,pos,scale,parent,active,rot,comps) in nodes.items():
  if selected is not None and go not in selected:continue
  tr=comps[0];parent=parent if selected is None or parent in [nodes[x][6][0] for x in selected] else 0
  if shift:pos=(pos[0]-shift,pos[1],pos[2])
  result.append('--- !u!1 &'+str(go)+'\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n'+''.join('  - component: {fileID: '+str(c)+'}\n' for c in comps)+'  m_Layer: 0\n  m_Name: '+name+'\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: '+str(active)+'\n')
  result.append('--- !u!4 &'+str(tr)+'\nTransform:\n'+common(go)+'  serializedVersion: 2\n  m_LocalRotation: '+vec(rot,'xyzw')+'\n  m_LocalPosition: '+vec(pos)+'\n  m_LocalScale: '+vec(scale)+'\n  m_ConstrainProportionsScale: 0\n  m_Children:'+('\n'+''.join('  - {fileID: '+str(c)+'}\n' for c in children[tr]) if children[tr] else ' []\n')+'  m_Father: {fileID: '+str(parent)+'}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n')
 return ''.join(result)
scene=root/'Assets/Scenes/BossFight.unity'
scene.write_text(head+serialize_nodes()+''.join(out)+'--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n'+''.join('  - {fileID: '+str(t)+'}\n' for t in roots),encoding='utf-8');guid(scene)
selection={g for g,n in nodes.items() if g==tablego or n[3]==table}
pref=root/'Assets/Resources/BossFight/FeltHockeyTable.prefab'
pref.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+serialize_nodes(selection,1000 if False else 0)+''.join(b for b in out if any('m_GameObject: {fileID: '+str(g)+'}' in b for g in selection)),encoding='utf-8')
# Prefab table origin zero; scene root remains x1000.
pref.write_text(pref.read_text(encoding='utf-8').replace('m_LocalPosition: {x: 1000, y: 0, z: 0}','m_LocalPosition: {x: 0, y: 0, z: 0}'),encoding='utf-8');guid(pref)
# Portable indexed OBJ with deliberate material groups, same exact table geometry.
obj=['# Felt air hockey table. Unity metres; Y up.','mtllib FeltHockeyTable.mtl'];idx=1
for n,p,s,m in tableparts:
 obj+=['o '+n.replace(' ','_'),'usemtl '+m]
 for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
  obj.append('v '+str(p[0]+x*s[0]/2)+' '+str(p[1]+y*s[1]/2)+' '+str(p[2]+z*s[2]/2))
 for f in [(1,4,3,2),(5,6,7,8),(1,2,6,5),(4,8,7,3),(1,5,8,4),(2,3,7,6)]:obj.append('f '+' '.join(str(idx+i-1) for i in f))
 idx+=8
(art/'FeltHockeyTable.obj').write_text('\n'.join(obj)+'\n',encoding='utf-8')
(art/'FeltHockeyTable.mtl').write_text('\n'.join('newmtl '+n+'\nKd '+' '.join(map(str,c[:3]))+'\nNs 10' for n,c in colors.items()),encoding='utf-8')
guid(art/'FeltHockeyTable.obj');guid(art/'FeltHockeyTable.mtl')
print('Authored',scene,'with',len(nodes),'GameObjects. Match manager fileID',managerid)
