// Local lobby introduction. Assign the compiled LobbyIntroduction.json in LobbyManager.
// Typed story functions follow the same object-tag pattern as MoveTo and SetCamera.
EXTERNAL SetVisible(objectTag, visible)
EXTERNAL SetVariant(objectTag, variantTag)
EXTERNAL PushTo(panelTag, seconds)
EXTERNAL MoveUI(objectTag, x, y, seconds)
EXTERNAL Knock(objectTag, count, interval)
EXTERNAL Wait(seconds)
EXTERNAL ExpandPanel(panelTag, seconds)
EXTERNAL FlashFrames(frames)
EXTERNAL DollyTo(sceneTag, amount, seconds)

// Still background with a cloud that changes to cloud_lightning.
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
~ MoveUI("letter", 40, 50, 1.8)
~ Wait(0.7)

// Push in the reading section and hold all three rhombus sections together.
~ SetVisible("letter_read", true)
~ PushTo("letter_read", 0.8)
~ Wait(5.0)
~ ExpandPanel("letter_fullscreen", 4.0)
~ Wait(2.0)
~ FlashFrames(2)
~ DollyTo("gate_close", 1.0, 5.0)
~ Wait(2.0)
-> END
