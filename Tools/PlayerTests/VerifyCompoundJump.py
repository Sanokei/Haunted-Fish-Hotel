from pathlib import Path
import re,math
p=Path('Assets/Resources/HotelNetworkPlayer.prefab').read_text()
f=lambda n:float(re.search(r'^  '+n+r': ([\d.]+)',p,re.M)[1])
g,j,run,walk,height,radius,skin=[f(n) for n in ['JumpGravity','JumpSpeed','RunningSpeed','WalkingSpeed','m_Height','m_Radius','m_SkinWidth']]
rows=[]
for fps in [30,60,120]:
 for direction in [1,-1]:
  for speed in [walk,run]:
   for toward in [0,5]:
    wins=[];peak=0
    for launch in [1.78+i*.01 for i in range(1800)]:
     x=-direction*launch;y=0;v=j;cx=0;blocked=False;cross=False
     for k in range(fps*4):
      dt=1/fps;v-=g*dt;y+=v*dt;x+=direction*speed*dt;cx=max(-3.5,min(3.5,cx-direction*toward*dt))
      # Conservative ceiling-contact tolerance; cancel velocity rather than hang.
      if y>4.11-height-skin:y=4.11-height-skin;v=min(v,0)
      peak=max(peak,y)
      if y<0:break
      for left,right,top in [(-1.25,1.25,1.7),(.29,1.25,2)]:
       if x+radius+skin>cx+left and x-radius-skin<cx+right and y<top+skin:blocked=True
      if (x-cx)*direction>1.25+radius+skin:cross=True
     if cross and not blocked:wins.append(launch)
    rows.append(f'FPS={fps} direction={direction} speed={speed} approaching={toward}: {len(wins)} clear launch distances; range={min(wins) if wins else None}..{max(wins) if wins else None}; conservative apex={peak:.4f}')
    if speed==run and (not wins or max(wins)-min(wins)<.7):raise Exception(rows[-1])
Path('Tools/PlayerTests/Evidence/2026-10-10-jump/compound-simulation.txt').write_text('\n'.join(rows)+'\nSimulation only: inflated rectangular capsule envelope and ceiling-stop model, no native collision or Play Mode acceptance.\n')
print('\n'.join(rows))
