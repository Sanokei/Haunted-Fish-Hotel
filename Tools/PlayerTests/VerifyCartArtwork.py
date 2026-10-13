"""Current tablecart artwork guard; historical native evidence remains hash-pinned."""
from pathlib import Path
import runpy
root=Path(__file__).resolve().parents[2]
runpy.run_path(str(root/'Tools/TrapBehaviorTests/verify_trap_artwork.py'),run_name='__main__')
motor=(root/'Assets/Scripts/Multiplayer/GameSideScrollMotor.cs').read_text()
movement=(root/'Assets/Scripts/Multiplayer/HotelPlayerMovement.cs').read_text()
actor=(root/'Assets/Scripts/Multiplayer/HotelPlayer.cs').read_text()
assert 'CollisionFlags.Above' in motor and ') _Vertical = 0;' in motor
assert 'if (_Controller.isGrounded)' in motor and '_PendingJump = false;' in motor
assert '_PendingJump && _Movement.CanMove' in motor and '_Movement.SharedDialogueLocked' in motor
assert 'Time.frameCount > _ResumeAfterFrame' in movement and '_InputBlocked' in movement
assert '(ControlMode != HotelControlMode.Fish || RoundReleased)' in actor
print('PASS: current artwork guard plus grounded/pending-jump/readiness/menu/grace/ceiling source checks. Old native jump evidence is stale after this new silhouette; rerun native validation.')
