"""Authored-geometry ballistic oracle. No PhysX/contact solver or native evidence."""
from pathlib import Path
import sys, re, unittest, math, yaml
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tools/ScenePresentationTests'))
from verify_scene_assets import records

def docs(path):
    return {r.ident: next(iter(yaml.safe_load(r.body).values())) for r in records((ROOT/path).read_text())}

class JumpGeometry(unittest.TestCase):
    def test_authored_tuning_and_stationary_cart_clearance(self):
        player=docs('Assets/Resources/HotelNetworkPlayer.prefab')
        motor=next(v for v in player.values() if '_RunningJumpSpeed' in v)
        movement=next(v for v in player.values() if 'RunningSpeed' in v)
        cc=player[21000004]
        self.assertEqual((motor['JumpSpeed'],motor['_RunningJumpSpeed'],movement['Gravity'],movement['RunningSpeed'],movement['WalkingSpeed']),(10.48,7.5,25,4.5,1.8))
        self.assertEqual((cc['m_Radius'],cc['m_Height']),(0.45,1.75))
        trap=docs('Assets/Resources/Traps/ShoppingCartTrap.prefab')
        halfheight=trap[31000094]['m_Size']['y']/2
        parts=[]
        for ref in trap[31000999]['_SolidParts']:
            c=trap[ref['fileID']]; p,s=c['m_Center'],c['m_Size']
            parts.append((p['x']-s['x']/2,p['x']+s['x']/2,halfheight+p['y']-s['y']/2,halfheight+p['y']+s['y']/2))
        jump,g,radius,half=10.48,25,cc['m_Radius'],cc['m_Height']/2
        segment=half-radius; duration=2*jump/g
        source=(ROOT/'Assets/Scripts/Multiplayer/GameHauntingController.cs').read_text()
        margin=float(re.search(r'ContactMargin = ([.\d]+)f',source)[1])
        def clearance(speed,launch,samples):
            best=float('inf')
            for sample in range(samples+1):
                t=duration*sample/samples;x=launch+speed*t;y=half+jump*t-g*t*t/2
                for left,right,bottom,top in parts:
                    dx=max(left-margin-x,0,x-right-margin)
                    dy=max(bottom-y-segment,0,y-segment-top)
                    best=min(best,math.hypot(dx,dy)-radius)
            return best
        def optimum(speed):
            return max((clearance(speed,-speed*jump/g+k*radius/50,240),-speed*jump/g+k*radius/50) for k in range(-50,51))
        old,old_start=optimum(4.5); new,new_start=optimum(7.5)
        dense=clearance(7.5,new_start,6000)
        self.assertLess(old,0)
        self.assertGreater(dense,.10)
        self.assertLess(abs(new-dense),.002)
        # Actual current ceiling still fits the exact unchanged vertical apex.
        self.assertLess(cc['m_Height']+jump*jump/(2*g),4.11)
        print(f'Analytic stationary cart: previous 4.5 best={old:.4f}m; 7.5 dense clearance={dense:.4f}m at launch={new_start:.4f}m; native physics pending')

if __name__=='__main__':unittest.main()
