# Haunted Intertitle

Original typeface authored for Haunted Fish Hotel from the approved generated
silent-film title-card direction. The `.ttf` is generated from the readable
centre-line and filled-serif geometry in `build_font.py`. It is not a renamed,
modified, or traced Smythe font. No source font is read by the authoring code.

## Build and check

Use a virtual environment and install `requirements.txt`, then, from the
repository root:

```sh
python Tools/IntertitleFont/build_font.py
python Tools/IntertitleFont/test_font.py
python Tools/IntertitleFont/proof_glyphs.py
```

The build writes `Assets/Art/Fonts/HauntedIntertitle-Regular.ttf`, the proof sheet,
and `Proofs/validation.json`. Optionally add `--reference /path/to/approved.png`
to create `Proofs/HauntedIntertitle-reference-comparison.png`. The optional image
is only placed beside actual TTF-rendered text; no reference pixels are sampled
or traced into glyph geometry. The font's timestamps are fixed for byte-identical
rebuilds under the pinned dependencies. Unity's checked-in font `.meta` must be
preserved when rebuilding.

## Design

- Family: **Haunted Intertitle**, one Regular face, 1000 units/em.
- Nominal cap height 760, x-height 520; ascenders extend to about 800.
- Ascent 850, descent -250, line gap 40. Use the matching typographic metrics.
- Tall, narrow letterforms, asymmetric bracketed serifs, lightly bowed/tapered
  stems, diagonal `e` crossbar, and selective curls on `G F a s t h`.
- Widths are softened 10% from the construction skeleton for title readability.
- 115 Unicode mappings and 116 glyphs (English-centric coverage; accented
  alphabets are not included): printable ASCII, NBSP, typographic quotes,
  en/em dashes, ellipsis, bullet, arrows, copyright/registered, degree, multiply,
  divide and minus. All current Fish/Ghost caption strings are covered.
- 67 explicitly designed pair adjustments, identical in `kern` and GPOS.
- Tabular lining figures. Space/NBSP use 209-unit advances.
- No alternate fonts, borrowed outlines, ligature replacement or runtime fallback
  are required for the supported guide strings.

The reference image contains different lettering proportions across its two
lines. This font follows that direction with one coherent, reusable alphabet;
it is not claimed to be an exact extraction of a complete font from the image.

## Verification

The generator normalizes each component's winding, unions overlapping shapes,
converts curves to TrueType quadratic contours with clockwise filled outlines
and counterclockwise holes, and reloads the resulting font.
The final pass checks all current guide captions, printable ASCII, contour
serialization, vertical bounds, and FreeType rendering at 24/36/48/96/240 px.
The standalone regression suite also checks SFNT checksum, original naming,
Unicode punctuation coverage, tabular figures, exact `kern`/GPOS agreement, and
stable Unity GUID, canonical contour direction and absence of tiny contours. Final rendered proofs were visually inspected for paragraph
readability, open counters, serif joins and curled terminals. There are no tiny
contours below 8 square design units in the final TTF.

`Proofs/HauntedIntertitle-proof.png` is an actual TTF rendering, not image-generated
lettering. `Proofs/HauntedIntertitle-reference-comparison.png` shows the approved
visual reference beside the actual font. These are font proofs, not Unity or
runtime screenshots.

TextMeshPro baking and integration are documented in
`../Fonts/TEXTMESHPRO.md`. Rebuild that asset after changing any glyph or metric.

`Proofs/HauntedIntertitle-glyph-inventory.png` shows every mapped codepoint
individually at 100 px with its Unicode label and baseline.
