// Local lobby introduction. Assign the compiled LobbyIntroduction.json in LobbyManager.
// Typed story functions follow the same object-tag pattern as MoveTo and SetCamera.
EXTERNAL SetVisible(objectTag, visible)
EXTERNAL SetVariant(objectTag, variantTag)
EXTERNAL PushTo(panelTag, seconds)
EXTERNAL MoveUI(objectTag, x, y, seconds)
EXTERNAL SlideUI(objectTag, x, y, seconds)
EXTERNAL Knock(objectTag, count, interval)
EXTERNAL Wait(seconds)
EXTERNAL ExpandPanel(panelTag, seconds)
EXTERNAL FlashFrames(frames)
EXTERNAL ParallaxTo(rigTag, poseTag, seconds, easing)
EXTERNAL LobbyShot(shotTag, seconds)
EXTERNAL LobbyLine(text, seconds)

// Still background with a cloud that changes to cloud_lightning.
// Reset the previous flash/reading reveal before replaying the opening.
~ SetVisible("exterior", false)
~ SetVisible("exterior_set", false)
~ SetVisible("letter_read", false)
~ SetVisible("background", true)
~ SetVisible("storm", true)
~ SetVisible("interior", false)
~ SetVariant("cloud", "cloud")
~ Wait(1.5)
~ SetVariant("cloud", "cloud_lightning")
~ Wait(0.8)

// Three knocks on the door.
~ SetVisible("storm", false)
~ SetVisible("interior", true)
~ Knock("door", 3, 0.45)
~ Wait(0.5)

// Push in a second rhombus section; keep the door visible beside the sliding letter.
~ PushTo("letter_under_door", 0.8)
// Shoved from outside: it pokes out, catches on the threshold, then the shove sends it gliding across the carpet until friction stops it.
~ MoveUI("letter", -58, 264, 0.3)
~ SlideUI("letter", 40, 50, 1.45)
~ Wait(0.75)

// Push in the reading section and hold all three rhombus sections together.
~ SetVisible("letter_read", true)
~ PushTo("letter_read", 0.8)
~ Wait(5.0)
~ ExpandPanel("letter_fullscreen", 4.0)
~ Wait(2.0)
// Reset the authored wide camera before the reveal, then crane down and push in together.
// Negative camera Y lifts foreground layers faster than the distant hotel.
~ ParallaxTo("exterior", "wide", 0.0, "linear")
~ FlashFrames(2)
~ ParallaxTo("exterior", "gate_arrival", 5.0, "smoother")
// Sound waits for the crane to settle on the gate: unlock, then the hinge squeals all the way open.
~ SetVisible("sfx_lock", true)
~ Wait(1.2)
~ SetVisible("sfx_gate", true)
~ Wait(4.6)
~ SetVisible("sfx_door_open", true)
// Reveal the real lobby: fish in front of camera, walk away, then camera passes fish toward reception.
// The 3D exterior set stops filming once the lobby takes over.
~ SetVisible("exterior_set", false)
~ LobbyShot("entry", 1.5)
~ LobbyShot("walk_away", 4.0)
~ LobbyLine("Looks like nobody has checked into this place for years", 4.5)
// One unbroken camera pass: dolly past the fish toward reception, then crane and tilt up into the frame tunnel.
~ LobbyShot("tunnel", 7.6)
~ Wait(1.0)
-> END
