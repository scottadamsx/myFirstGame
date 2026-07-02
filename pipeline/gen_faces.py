#!/usr/bin/env python3
"""Face atlas for NPCs: 2x2 grid of 256px stylized faces (RGBA, transparent bg)
applied to a quad on each character's head. Variants: 0 friendly, 1 laughing,
2 bearded, 3 glasses."""
import pathlib

from PIL import Image, ImageDraw

DEST = pathlib.Path(__file__).parent.parent / "game" / "Assets" / "Resources" / "City"
DEST.mkdir(parents=True, exist_ok=True)

CELL = 256
atlas = Image.new("RGBA", (CELL * 2, CELL * 2), (0, 0, 0, 0))


def draw_face(cx, cy, variant):
    d = ImageDraw.Draw(atlas)
    ex, ey = 46, -14          # eye offset from center
    ew, eh = 46, 32           # eye size

    def eye(sx):
        x, y = cx + sx * ex, cy + ey
        d.ellipse([x - ew // 2, y - eh // 2, x + ew // 2, y + eh // 2], fill=(250, 250, 250, 255))
        iris = [(96, 64, 40), (70, 90, 120), (80, 110, 70)][variant % 3]
        d.ellipse([x - 13, y - 13, x + 13, y + 13], fill=iris + (255,))
        d.ellipse([x - 6, y - 6, x + 6, y + 6], fill=(20, 20, 22, 255))
        d.ellipse([x + 2, y - 8, x + 9, y - 1], fill=(255, 255, 255, 220))

    eye(-1)
    eye(1)

    # brows
    bw = 46
    by = cy - 52
    tilt = {0: 4, 1: 8, 2: 2, 3: 5}[variant]
    d.line([cx - ex - bw // 2, by + tilt, cx - ex + bw // 2, by - tilt], fill=(60, 45, 35, 255), width=10)
    d.line([cx + ex - bw // 2, by - tilt, cx + ex + bw // 2, by + tilt], fill=(60, 45, 35, 255), width=10)

    # mouth
    my = cy + 58
    if variant == 1:      # laughing — open smile
        d.pieslice([cx - 40, my - 26, cx + 40, my + 30], 10, 170, fill=(120, 55, 55, 255))
        d.pieslice([cx - 30, my - 20, cx + 30, my + 6], 15, 165, fill=(245, 245, 245, 255))
    elif variant == 2:    # bearded — modest line
        d.line([cx - 26, my, cx + 26, my], fill=(90, 55, 50, 255), width=9)
    else:
        d.arc([cx - 38, my - 34, cx + 38, my + 18], 20, 160, fill=(120, 55, 55, 255), width=11)

    # rosy cheeks
    if variant in (0, 1):
        for sx in (-1, 1):
            d.ellipse([cx + sx * 78 - 18, cy + 26 - 12, cx + sx * 78 + 18, cy + 26 + 12], fill=(225, 130, 120, 70))

    # beard
    if variant == 2:
        d.pieslice([cx - 78, cy - 10, cx + 78, cy + 118], 15, 165, fill=(75, 58, 45, 235))
        d.line([cx - 26, my, cx + 26, my], fill=(140, 90, 80, 255), width=8)

    # glasses
    if variant == 3:
        for sx in (-1, 1):
            x, y = cx + sx * ex, cy + ey
            d.ellipse([x - 34, y - 28, x + 34, y + 28], outline=(40, 40, 45, 255), width=7)
        d.line([cx - 12, cy + ey, cx + 12, cy + ey], fill=(40, 40, 45, 255), width=7)


for i, (gx, gy) in enumerate([(0, 0), (1, 0), (0, 1), (1, 1)]):
    draw_face(gx * CELL + CELL // 2, gy * CELL + CELL // 2, i)

atlas.save(DEST / "faces.png")
print("wrote", DEST / "faces.png")
