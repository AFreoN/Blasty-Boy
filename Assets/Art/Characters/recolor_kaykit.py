"""Palette-swap KayKit gradient atlases into the rooftop cast.

KayKit textures are an 8x4 grid of vertical gradient swatches. Each recipe maps swatches whose average hue falls in a
range to a new base color, keeping the swatch's own light-to-dark gradient. Re-run after tweaking recipes:
    python Assets/Art/Characters/recolor_kaykit.py
"""
import colorsys
from PIL import Image

SRC = "Assets/ThirdParty/KayKit/Characters/"
DST = "Assets/Art/Characters/"
COLS, ROWS = 8, 4


def hue_of(rgb):
    h, s, v = colorsys.rgb_to_hsv(*[c / 255 for c in rgb])
    return h * 360, s, v


def recolor(src, dst, rules):
    im = Image.open(SRC + src).convert("RGBA")
    w, h = im.size
    cw, ch = w // COLS, h // ROWS
    px = im.load()
    for row in range(ROWS):
        for col in range(COLS):
            box = (col * cw, row * ch, (col + 1) * cw, (row + 1) * ch)
            cell = im.crop(box).convert("RGB")
            avg = tuple(sum(c) / len(c) for c in zip(*cell.getdata()))
            hue, sat, val = hue_of(avg)
            for (lo, hi, min_sat, target, strength) in rules:
                if lo <= hue <= hi and sat >= min_sat:
                    tr, tg, tb = target
                    for y in range(box[1], box[3]):
                        for x in range(box[0], box[2]):
                            r, g, b, a = px[x, y]
                            lum = (0.3 * r + 0.59 * g + 0.11 * b) / max(1, 0.3 * avg[0] + 0.59 * avg[1] + 0.11 * avg[2])
                            nr, ng, nb = (min(255, int(tr * lum)), min(255, int(tg * lum)), min(255, int(tb * lum)))
                            px[x, y] = (int(r + (nr - r) * strength), int(g + (ng - g) * strength), int(b + (nb - b) * strength), a)
                    break
    im.save(DST + dst)
    print("wrote", dst)


GREEN = (90, 200)      # KayKit rogue cloth
LEATHER = (10, 40)     # browns
STEEL = (180, 260)     # knight blue-grey

# Ninja hero: black cloth, deep red leather accents.
recolor("rogue_texture.png", "ninja_texture.png", [(GREEN[0], GREEN[1], 0.25, (38, 40, 52), 1.0), (LEATHER[0], LEATHER[1], 0.5, (150, 30, 40), 0.85)])
# Gang goons: gang purple cloth, dark leather.
recolor("rogue_texture.png", "goon_texture.png", [(GREEN[0], GREEN[1], 0.25, (120, 50, 170), 1.0), (LEATHER[0], LEATHER[1], 0.5, (70, 50, 45), 0.7)])
# Runners: same gang, hi-vis orange so the fast ones read instantly.
recolor("rogue_texture.png", "runner_texture.png", [(GREEN[0], GREEN[1], 0.25, (240, 120, 30), 1.0), (LEATHER[0], LEATHER[1], 0.5, (70, 50, 45), 0.7)])
# Riot goons: dark steel armor with gang purple trim.
recolor("knight_texture.png", "riot_texture.png", [(STEEL[0], STEEL[1], 0.05, (70, 78, 95), 0.9), (330, 360, 0.4, (120, 50, 170), 1.0), (0, 12, 0.4, (120, 50, 170), 1.0)])
# Boss "Big Bear": keeps the bear hat, gang purple and gold.
recolor("barbarian_texture.png", "boss_texture.png", [(330, 360, 0.4, (120, 50, 170), 1.0), (0, 8, 0.5, (120, 50, 170), 1.0), (40, 65, 0.4, (235, 185, 40), 1.0)])
