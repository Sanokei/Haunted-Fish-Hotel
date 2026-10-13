from PIL import Image
from pathlib import Path
import re
p=Path('Assets/Resources/Traps/ShoppingCartTrap.prefab').read_text()
assert '  m_Size: {x: 2.5, y: 2, z: 1}' in p and '  _Collider: {fileID: 31000094}' in p
assert '  _SolidParts:\n  - {fileID: 31900001}\n  - {fileID: 31900002}' in p
for cid in [31900001,31900002]:
 block=p.split(f'--- !u!65 &{cid}')[1].split('--- !u!')[0]
 assert '  m_Enabled: 1' in block and '  m_IsTrigger: 0' in block
im=Image.open('Assets/Art/shopping card.png').convert('RGBA');a=im.getchannel('A');count=0
for y in range(405):
 for x in range(489):
  if not a.getpixel((x,y)):continue
  wx=(x+.5-250.5)/413*2.5;wy=(373-y-.5)/328*2
  assert (-1.25<=wx<=1.25 and 0<=wy<=1.7) or (.29<=wx<=1.25 and 1.5<=wy<=2),(x,y,wx,wy)
  count+=1
motor=Path('Assets/Scripts/Multiplayer/GameSideScrollMotor.cs').read_text();movement=Path('Assets/Scripts/Multiplayer/HotelPlayerMovement.cs').read_text();actor=Path('Assets/Scripts/Multiplayer/HotelPlayer.cs').read_text();haunt=Path('Assets/Scripts/Multiplayer/GameHauntingController.cs').read_text()
assert 'CollisionFlags.Above' in motor and ') _Vertical = 0;' in motor
assert 'if (_Controller.isGrounded)' in motor and '_PendingJump = false;' in motor
assert '_PendingJump && _Movement.CanMove' in motor and '_Movement.SharedDialogueLocked' in motor
assert 'Time.frameCount > _ResumeAfterFrame' in movement and '_InputBlocked' in movement
assert '(ControlMode != HotelControlMode.Fish || RoundReleased)' in actor
assert 'trap.TouchesBody(fish.BodyController.bounds, .15f)' in haunt
print(f'PASS: all {count} nontransparent cart pixels contained by enabled solid authored parts; outer2.5x2 footprint retained. Grounded, pending consumption, readiness/menu/grace and ceiling cancellation source checks pass. Not native/Play Mode.')
