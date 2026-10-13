from pathlib import Path
import numpy as np, math
radius,skin,height=.45,.08,1.75
rows=[]
for g in [25,20,16,12,10,8,6,4,2.5]:
 v0=math.sqrt(2*g*2.275);air=2*v0/g; margins=[];walk=0; peaks=[]
 for fps in [30,60,120]:
  for direction in [1,-1]:
   for speed in [1.8,4.5]:
    for toward in [0,5]:
     launches=np.arange(1.8,16,.01);blocked=np.zeros(launches.size,dtype=bool);crossed=blocked.copy();y=0.;v=v0;cx=0.;peak=0
     for k in range(int(fps*4)):
      dt=1/fps;oldY=y;oldCX=cx;v-=g*dt;y+=v*dt;cx=max(-3.5,min(3.5,cx-direction*toward*dt));t=(k+1)*dt
      if y>4.11-height-skin:y=4.11-height-skin;v=min(v,0)
      peak=max(peak,y)
      if y<0:break
      for a in [.25,.5,.75,1]:
       sy=oldY+(y-oldY)*a;scx=oldCX+(cx-oldCX)*a;sx=direction*(speed*(t-dt+dt*a)-launches)
       for left,right,top in [(-1.25,1.25,1.7),(.29,1.25,2)]:
        dx=np.maximum(np.maximum(scx+left-sx,sx-scx-right),0);dy=max(sy+radius-top,0)
        blocked|=(dx*dx+dy*dy<(radius+skin)**2)
      x=direction*(speed*t-launches);crossed|=(x-cx)*direction>1.25+radius+skin
     wins=launches[crossed&~blocked];window=(float(wins[-1]-wins[0]) if len(wins) else 0)
     if speed>2:margins.append(window)
     else:walk+=bool(len(wins))
     peaks.append(peak)
 minimum=min(margins);maximum=max(margins)
 rows.append(f'gravity={g:4.1f} jump={v0:7.4f} free_apex=2.275 airtime={air:.3f}s discrete_apex={min(peaks):.4f}..{max(peaks):.4f} min_run_launch_window={minimum:.3f}m ({minimum/4.5:.3f}s) max_window={maximum:.3f}m normal_walk_cases={walk}/12')
Path('Tools/PlayerTests/Evidence/2026-10-10-jump/pair-comparison.txt').write_text('\n'.join(rows)+'\nRounded capsule + skin Minkowski-distance simulation, four linear-motion sweep samples/frame. Not CharacterController/native acceptance.\n')
print('\n'.join(rows))
