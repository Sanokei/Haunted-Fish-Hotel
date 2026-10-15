"""Authoring-time pose functions for the boss's head and eight tentacles.

Each clip is a function (part, t) -> (rotation_degrees, dx, dy) in the part's local space, t in seconds. The same
functions feed the preview sheet and the AnimationClip generator, so what is reviewed is what ships.

Tentacles are numbered so the outer ones (2) are the long reachers and the inner ones (1, 3) hug the body.
Left arms are Left tentacle 1..4 and mirror the right arms: positive rotation is counter-clockwise in Unity, so
`side` flips the sign to keep left/right symmetric.
"""
import math

TAU = math.tau
ARMS = [f'{s} tentacle {i}' for s in ('Left', 'Right') for i in range(1, 5)]
PARTS = ['Head'] + ARMS


def side(arm):
    return 1 if arm.startswith('Left') else -1


def index(arm):
    return int(arm.split()[-1])


def phase(arm):
    return (index(arm) * 1.9 + (0 if arm.startswith('Left') else 2.3))


def reach(arm):
    """Outer tentacles travel further; inner ones stay close to the body."""
    return {1: .75, 2: 1.25, 3: .6, 4: 1.0}[index(arm)]


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def pulse(x):
    """0 -> 1 -> 0 over x in [0, 1]."""
    return math.sin(math.pi * max(0.0, min(1.0, x)))


# ---- looping states -------------------------------------------------------------------------------------------

def idle(part, t, length=6.0):
    w = TAU * t / length
    if part == 'Head':
        return (1.6 * math.sin(w), 0.0, .025 * math.sin(2 * w))
    s, r = side(part), reach(part)
    # a slow ripple that passes down each side, plus an occasional lazy curl toward the middle
    sway = 7.0 * r * math.sin(w + phase(part))
    curl = 9.0 * r * max(0.0, math.sin(2 * w + phase(part) * 1.3)) ** 3
    return (s * (sway + curl), .02 * r * math.sin(w + phase(part)), 0.0)


def juggle(part, t, length=2.0):
    """The boss is passing a hot cue between hands: sides alternate, arms lift and snap."""
    w = TAU * t / length
    if part == 'Head':
        return (4.0 * math.sin(w), .03 * math.sin(w), .05 * abs(math.sin(w)))
    s, r = side(part), reach(part)
    alt = math.sin(w + (0 if s == 1 else math.pi))
    lift = max(0.0, alt)
    return (s * (-16 * r * alt + 4 * math.sin(2 * w + phase(part))), 0.06 * r * alt, .16 * r * lift)


def panic(part, t, length=0.8):
    """Fuse nearly out: every arm trembles and flails, the head shakes."""
    w = TAU * t / length
    if part == 'Head':
        return (6.0 * math.sin(2 * w), .04 * math.sin(3 * w), .03 * math.sin(4 * w))
    s, r = side(part), reach(part)
    return (s * (14 * r * math.sin(2 * w + phase(part)) + 6), .05 * r * math.sin(3 * w + phase(part)), .14 * r + .05 * math.sin(4 * w + phase(part)))


# ---- one-shot reactions ---------------------------------------------------------------------------------------

def frustrated(part, t, length=2.4):
    """The fish scored. Tentacles rear back one after another, then slam the table; the head shudders."""
    u = t / length
    if part == 'Head':
        return (5.0 * math.sin(u * math.pi * 10) * pulse(u), 0.0, -.10 * pulse(min(1.0, u * 2)))
    s, r = side(part), reach(part)
    start = {1: 0.0, 2: .08, 3: .16, 4: .24}[index(part)] + (.06 if s == -1 else 0.0)
    k = (u - start) / .55
    if k <= 0 or k >= 1:
        return (0.0, 0.0, 0.0)
    if k < .55:                                   # wind up: rear back and lift
        w = smooth(k / .55)
        return (s * 34 * r * w, 0.0, .38 * r * w)
    if k < .75:                                   # strike: whip down past rest
        q = smooth((k - .55) / .2)
        return (s * (34 * r * (1 - q) - 14 * r * q), 0.0, .38 * r * (1 - q) - .08 * q)
    q = smooth((k - .75) / .25)                   # settle
    return (s * -14 * r * (1 - q), 0.0, -.08 * (1 - q))


def gloat(part, t, length=2.4):
    """The ghost scored. A wave runs along the tentacles and the head bobs, delighted."""
    u = t / length
    if part == 'Head':
        return (7.0 * math.sin(u * math.pi * 6), 0.0, .12 * abs(math.sin(u * math.pi * 3)))
    s, r = side(part), reach(part)
    order = (index(part) - 1) / 3.0 + (0.0 if s == 1 else .12)
    wave = math.sin((u * 3.0 - order) * TAU)
    env = pulse(u) ** .6
    return (s * 22 * r * wave * env, .05 * r * wave * env, .14 * r * max(0.0, wave) * env)


def flinch(part, t, length=1.2):
    """A bomb went off on the boss's half: arms fold over the head, a hard duck, then a slow recovery."""
    u = t / length
    snap = smooth(u * 8) * (1 - smooth((u - .35) * 1.6))
    if part == 'Head':
        return (-8.0 * snap * math.sin(u * 30) * (1 - u), 0.0, -.2 * snap)
    s, r = side(part), reach(part)
    return (s * -40 * r * snap, 0.0, .3 * r * snap)


def defeat(part, t, length=3.0):
    """The boss loses its last life: tentacles go limp, arc outward and sink, the head droops. Holds the last pose."""
    u = smooth(t / length)
    if part == 'Head':
        return (-9.0 * u, 0.0, -.42 * u)
    s, r = side(part), reach(part)
    return (s * (46 * r + 8 * index(part)) * u, -.05 * u, -.5 * u)


def triumph(part, t, length=2.0):
    """The boss wins: every arm thrown up and wide, rippling fast; the head pulses with glee."""
    w = TAU * t / length
    if part == 'Head':
        return (5.0 * math.sin(2 * w), 0.0, .1 + .06 * math.sin(2 * w))
    s, r = side(part), reach(part)
    return (s * (-30 * r + 12 * r * math.sin(2 * w + phase(part))), .06 * math.sin(2 * w + phase(part)), .3 * r)


# name -> (function, length seconds, loops)
CLIPS = {
    'Idle': (idle, 6.0, True),
    'Juggle': (juggle, 2.0, True),
    'Panic': (panic, 0.8, True),
    'Frustrated': (frustrated, 2.4, False),
    'Gloat': (gloat, 2.4, False),
    'Flinch': (flinch, 1.2, False),
    'Defeat': (defeat, 3.0, False),
    'Triumph': (triumph, 2.0, True),
}


def sample(clip, part, t):
    fn, length, loops = CLIPS[clip]
    return fn(part, t % length if loops else min(t, length))
