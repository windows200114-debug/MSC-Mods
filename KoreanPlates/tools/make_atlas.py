#!/usr/bin/env python3
"""Renders the glyph atlas (digits + hangul plate letters) embedded in KoreanPlates.
Usage: python3 make_atlas.py HANGUL_FONT.ttf [index] [DIGIT_FONT.ttf]  ->  ../glyphs.png
Atlas: one row of 128x192 cells, white glyph on transparent bg (alpha = coverage).
Cell order is fixed by CHARS and must match KoreanPlates.Chars in the mod."""
import sys
from PIL import Image, ImageDraw, ImageFont

CHARS = "0123456789" + "가나다라마거너더러머버서어저고노도로모보소오조구누두루무부수우주하허호" + "KOR"
W, H = 128, 192
font = ImageFont.truetype(sys.argv[1], 130, index=int(sys.argv[2]) if len(sys.argv) > 2 else 0)
digit_font = ImageFont.truetype(sys.argv[3], 140) if len(sys.argv) > 3 else font
img = Image.new("RGBA", (W * len(CHARS), H), (255, 255, 255, 0))
d = ImageDraw.Draw(img)
for i, ch in enumerate(CHARS):
    f = digit_font if ch.isdigit() else font
    l, t, r, b = d.textbbox((0, 0), ch, font=f)
    x = i * W + (W - (r - l)) // 2 - l
    y = (H - (b - t)) // 2 - t
    # draw into alpha only, thicken slightly for the heavy plate look
    d.text((x, y), ch, font=f, fill=(255, 255, 255, 255), stroke_width=(2 if ch.isdigit() else 6), stroke_fill=(255, 255, 255, 255))
img.save("../glyphs.png")
print(len(CHARS), img.size)
