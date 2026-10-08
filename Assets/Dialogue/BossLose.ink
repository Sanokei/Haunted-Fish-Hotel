EXTERNAL MoveUI(tag, x, y, seconds)
EXTERNAL Spin(tag, turns, seconds)
EXTERNAL Wait(seconds)
~ MoveUI("actor", 0, -0.25, 0.15)
~ MoveUI("actor", 0, 0.25, 0.15)
~ MoveUI("actor", 0, 0, 0.15)
~ Spin("actor", -1, 0.4)
~ Wait(0.15)
-> END
