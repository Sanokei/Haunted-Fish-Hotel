"""Authored silent-film frame artwork, in the approved fan/botanical vocabulary.

All contours below are original cubic-vector paths. They are rasterized at the
card renderer's working scale, not copied from or painted over a reference.
Opening cards use the complete engraving; instruction cards use its small fan
and paired curved stems. No rounded rectangles or mathematical spiral stamps.
"""
from functools import lru_cache
from math import hypot

from PIL import Image, ImageDraw

BORDER_INK = (215, 202, 173)
BORDER_RULE = (198, 187, 160)
ART_VERSION = 'botanical-fan-v1'


def _points(path, steps=40):
    """Flatten a compact M/L/C/Z path without an optional imaging dependency."""
    result = []
    current = None
    for command in path:
        if command[0] == 'M':
            current = command[1:3]
            result.append(current)
        elif command[0] == 'L':
            current = command[1:3]
            result.append(current)
        elif command[0] == 'C':
            x0, y0 = current
            x1, y1, x2, y2, x3, y3 = command[1:]
            for n in range(1, steps + 1):
                t = n / steps
                u = 1 - t
                result.append((u*u*u*x0 + 3*u*u*t*x1 + 3*u*t*t*x2 + t*t*t*x3,
                               u*u*u*y0 + 3*u*u*t*y1 + 3*u*t*t*y2 + t*t*t*y3))
            current = (x3, y3)
        elif command[0] != 'Z':
            raise ValueError('Unsupported artwork command: ' + command[0])
    return result


class _Engraving:
    def __init__(self, image, scale, transform=lambda x, y: (x, y)):
        self.draw = ImageDraw.Draw(image)
        self.scale = scale
        self.transform = transform

    def _coords(self, points):
        return [(round(a*self.scale, 3), round(b*self.scale, 3))
                for a, b in (self.transform(x, y) for x, y in points)]

    def fill(self, path, ink=BORDER_INK):
        self.draw.polygon(self._coords(_points(path)), fill=ink)

    def rule(self, points, width=1.2):
        self.draw.line(self._coords(points), fill=BORDER_RULE,
                       width=max(1, round(width*self.scale)))

    def stroke(self, path, widths=(1.5, 3.0, 1.0), ink=BORDER_INK):
        """A filled ribbon with a smooth, tapered stroke, not a monoline curl."""
        points = _points(path)
        distances = [0.0]
        for a, b in zip(points, points[1:]):
            distances.append(distances[-1] + hypot(b[0]-a[0], b[1]-a[1]))
        if not distances[-1]:
            return
        left, right = [], []
        for i, ((x, y), distance) in enumerate(zip(points, distances)):
            a, b = points[max(0, i-1)], points[min(len(points)-1, i+1)]
            dx, dy = b[0]-a[0], b[1]-a[1]
            length = hypot(dx, dy) or 1
            t = distance/distances[-1] * (len(widths)-1)
            index = min(int(t), len(widths)-2)
            blend = t - index
            blend = blend*blend*(3-2*blend)
            half_width = (widths[index]*(1-blend) + widths[index+1]*blend)/2
            normal = (-dy/length*half_width, dx/length*half_width)
            left.append((x+normal[0], y+normal[1]))
            right.append((x-normal[0], y-normal[1]))
        self.draw.polygon(self._coords(left + right[::-1]), fill=ink)


# Narrow common roots leave black channels between the nine fan blades.
_FAN = (
    [('M',94,126),('C',87,112,69,108,55,109),('C',49,109,49,103,54,102),('C',68,99,87,112,94,126),('Z',)],
    [('M',94,125),('C',86,108,67,100,53,100),('C',47,100,47,93,52,92),('C',67,91,87,109,94,125),('Z',)],
    [('M',94,124),('C',86,103,67,92,52,90),('C',46,89,49,80,54,81),('C',70,86,87,106,94,124),('Z',)],
    [('M',94,123),('C',86,99,68,83,54,77),('C',48,74,52,65,58,68),('C',73,77,88,103,94,123),('Z',)],
    [('M',95,123),('C',87,98,74,75,59,66),('C',52,61,59,51,65,55),('C',80,69,90,101,95,123),('Z',)],
    [('M',95,122),('C',90,98,81,69,69,56),('C',63,48,73,46,77,52),('C',86,70,93,101,95,122),('Z',)],
    [('M',95,121),('C',92,96,88,69,80,54),('C',75,45,85,46,88,51),('C',92,69,95,100,95,121),('Z',)],
    [('M',96,120),('C',96,94,96,72,91,55),('C',89,48,98,49,100,55),('C',101,71,97,99,96,120),('Z',)],
)


def _corner(art, ornate=True):
    for path in _FAN:
        art.fill(path)
    # The fan's spine flows into the lower inward scroll and the ruled edge.
    art.stroke([('M',96,121),('C',90,155,60,163,60,145),
                ('C',60,139,68,140,66,145)], (3.4,4.2,2.6,5.4,1.0))
    art.stroke([('M',96,125),('C',95,101,113,77,133,75),
                ('C',147,73,154,80,150,88),('C',146,96,135,92,138,85),
                ('C',139,82,144,83,143,87)], (4.1,2.8,1.7,3.3,5.2,0.5))
    art.stroke([('M',94,120),('C',82,99,58,105,43,116),
                ('C',27,126,25,108,32,105),('C',39,101,40,112,34,111)],
               (2.0,3.5,4.2,4.0,1.0))
    art.stroke([('M',94,122),('C',95,89,100,61,111,44),
                ('C',124,24,106,23,102,32),('C',98,44,114,43,109,35)],
               (2.4,2.8,3.1,4.7,1.4))
    # Paired long stems sit on the two straight frame rules.
    art.stroke([('M',47,177),('C',49,137,40,114,62,110),
                ('C',72,106,89,110,94,120)], (1.3,2.5,2.3,0.6))
    if not ornate:
        return
    art.stroke([('M',98,87),('C',117,45,151,43,175,54),
                ('C',199,67,219,50,238,55),('C',256,61,260,78,252,81),
                ('C',244,85,242,74,249,73)], (2.0,2.2,1.6,2.0,3.0,6.2,0.5))
    # Right-facing bay leaf: long pointed tip, wide body, narrowed stalk.
    art.fill([('M',190,58),('C',207,45,222,38,241,35),
              ('C',248,34,239,42,234,44),('C',218,52,206,53,190,58),('Z',)])
    art.fill([('M',182,58),('C',194,56,210,58,218,64),
              ('C',223,69,211,70,203,66),('C',194,62,190,60,182,58),('Z',)])
    art.stroke([('M',174,55),('C',181,68,184,73,187,83)], (1.8,1.1,0.5))
    art.stroke([('M',186,78),('C',190,80,193,80,195,82)], (1.1,0.8))
    art.fill([('M',188,77),('C',192,74,188,69,185,71),('C',182,74,185,78,188,77),('Z',)])
    art.fill([('M',184,84),('C',188,81,185,76,182,78),('C',178,81,181,86,184,84),('Z',)])
    art.fill([('M',194,86),('C',200,86,199,81,195,80),('C',191,79,191,85,194,86),('Z',)])
    # Upright leaf spray grows out of the same vertical rail, with a terminal bud.
    art.stroke([('M',46,157),('C',44,179,58,193,62,214),
                ('C',66,232,54,239,57,226),('C',58,222,62,225,60,228)],
               (1.4,2.1,1.5,2.5,4.8,0.6))
    art.stroke([('M',47,161),('C',49,180,60,195,77,208)], (2.0,1.6,0.3))
    art.fill([('M',45,172),('C',36,181,31,185,30,195),
              ('C',29,203,40,195,44,183),('C',46,178,47,174,49,170),('Z',)])
    art.fill([('M',51,179),('C',53,184,59,189,58,194),
              ('C',57,199,52,195,52,190),('C',52,185,52,183,51,179),('Z',)])
    art.stroke([('M',57,185),('C',60,180,62,178,66,178)], (1.1,0.6))
    art.fill([('M',69,181),('C',76,178,69,172,65,175),('C',61,178,65,182,69,181),('Z',)])
    art.stroke([('M',61,192),('C',66,188,68,186,74,186)], (1.1,0.4))
    art.fill([('M',76,189),('C',81,186,77,182,73,183),('C',68,184,71,190,76,189),('Z',)])
    art.stroke([('M',68,200),('C',70,195,73,194,78,195)], (1.1,0.4))
    art.fill([('M',80,198),('C',86,196,81,191,78,192),('C',73,193,76,199,80,198),('Z',)])
    art.fill([('M',79,211),('C',84,209,82,203,77,202),('C',71,201,74,211,79,211),('Z',)])


def _center(art):
    # Individual closed contours share only their narrow stem at (640, 671).
    # Unequal petals preserve the hand-engraved rhythm and visible black channels.
    for path in (
        [('M',640,671),('C',635,653,622,644,616,641),('C',607,637,612,628,618,632),('C',628,642,636,659,640,671),('Z',)],
        [('M',640,671),('C',637,652,626,637,621,629),('C',615,621,623,616,627,624),('C',633,639,638,657,640,671),('Z',)],
        [('M',640,671),('C',638,651,633,630,628,620),('C',624,611,633,608,635,617),('C',639,635,639,655,640,671),('Z',)],
        [('M',640,671),('C',640,653,638,626,636,614),('C',634,605,644,604,644,613),('C',643,631,641,655,640,671),('Z',)],
        [('M',640,671),('C',643,650,643,630,646,618),('C',648,608,656,611,653,620),('C',646,637,643,656,640,671),('Z',)],
        [('M',640,671),('C',645,650,651,637,656,625),('C',660,617,668,621,663,628),('C',652,641,644,659,640,671),('Z',)],
        [('M',640,671),('C',647,652,659,638,665,633),('C',672,628,677,636,669,640),('C',655,647,646,659,640,671),('Z',)],
    ):
        art.fill(path)
    art.fill([('M',640,669),('L',643,674),('L',640,680),('L',637,674),('Z',)])
    for side in (-1,1):
        def mirror(path):
            result=[]
            for command in path:
                result.append(tuple([command[0]] + [640+side*v if i%2==0 else v
                              for i,v in enumerate(command[1:])]))
            return result
        art.stroke(mirror([('M',12,663),('C',36,664,49,647,66,649),
                           ('C',89,650,116,673,135,657),('C',144,650,133,642,131,651)]),
                   (1.0,2.2,1.3,2.6,5.6,0.5))
        art.stroke(mirror([('M',25,661),('C',38,652,41,644,49,638)]), (1.3,1.4,0.4))
        art.fill(mirror([('M',33,659),('C',42,651,54,654,61,662),
                         ('C',62,665,48,665,42,662),('C',39,661,36,660,33,659),('Z',)]))
        art.fill(mirror([('M',45,645),('C',51,640,48,633,45,636),
                         ('C',41,639,44,643,45,645),('Z',)]))
        art.fill(mirror([('M',52,640),('C',58,639,58,632,54,633),
                         ('C',50,634,52,638,52,640),('Z',)]))
        art.stroke(mirror([('M',20,663),('C',34,660,42,649,59,644)]), (1.2,0.9,0.3))
        art.fill(mirror([('M',60,646),('C',67,645,64,639,60,640),
                         ('C',56,641,57,646,60,646),('Z',)]))


@lru_cache(maxsize=4)
def _render_frame(opening, output_scale):
    """Supersample the engraving independently of text, then cache its overlay."""
    scale = max(4, output_scale)
    width, height = 1280, 720
    image = Image.new('RGBA', (width*scale,height*scale))
    art = _Engraving(image, scale)
    # A true rectangular engraved frame. Stems overlap and continue these rules.
    art.rule([(43,43),(1237,43),(1237,677),(43,677),(43,43)], 1.8)
    art.rule([(49,49),(1231,49),(1231,671),(49,671),(49,49)], 1.0)
    corner_scale = 1 if opening else .62
    # Simplified corners remain attached at the same rail intersections.
    for right in (False, True):
        for bottom in (False, True):
            def transform(x, y, right=right, bottom=bottom):
                x = 43 + (x-43)*corner_scale
                y = 43 + (y-43)*corner_scale
                return (width-x if right else x, height-y if bottom else y)
            _corner(_Engraving(image, scale, transform), ornate=opening)
    if opening:
        _center(art)
    contract = {'version':ART_VERSION, 'style':'opening' if opening else 'instruction',
            'outer_rule':[43,43,1237,677], 'inner_rule':[49,49,1231,671],
            'corner_scale':corner_scale, 'text_safe_area':[115,100,1165,600]}

    if scale != output_scale:
        image = image.resize((width*output_scale,height*output_scale),Image.Resampling.LANCZOS)
    return image, contract


def draw_frame(image, opening, scale=2):
    """Draw smooth connected artwork and return its inspectable design contract."""
    if image.size != (1280*scale,720*scale):
        raise ValueError('The frame must use the card renderer working dimensions.')
    overlay, contract = _render_frame(opening,scale)
    image.paste(overlay,(0,0),overlay)
    return dict(contract)
