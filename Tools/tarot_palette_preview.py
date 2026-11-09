"""Fool palette preview.

The colour rules here are the same ones in Assets/Shaders/TarotPalette.shader.
Game frames are about 216px tall, so the in-game shader uses a fixed 6px eye
window (EYE_RADIUS_PX below). This script scales that window with image height
when previewing the tall reference portrait.

Run from the repo root:
  python3 Tools/tarot_palette_preview.py --out /opt/cursor/artifacts
Optional portraits:
  --original path/to/original_witch.png --fool-ref path/to/fool_form.png
"""
import colorsys
from PIL import Image

# Game frames are 216px tall. Eye guard radius scales with height so the
# portrait (1275px) uses the same relative neighbourhood.
REF_HEIGHT = 216.0
EYE_RADIUS_PX = 6.0


def hsv_of(r, g, b):
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    return h * 360.0, s, v


def from_hsv(h_deg, s, v):
    r, g, b = colorsys.hsv_to_rgb((h_deg % 360.0) / 360.0, max(0.0, min(1.0, s)), max(0.0, min(1.0, v)))
    return r, g, b


def is_skin(r, g, b, a):
    """Peach face / hands / legs. Gold stars and copper staff rings are not skin."""
    if a < 0.70:
        return False
    # Peach is clearly redder than it is green, and not a yellow highlight.
    if not (r > g + 0.035 and g + 0.02 >= b and r > 0.72 and b < 0.92):
        return False
    hue, s, v = hsv_of(r, g, b)
    return 4.0 <= hue <= 30.0 and 0.07 <= s <= 0.36 and v >= 0.70


def is_eye_core(r, g, b, a):
    """Bright azure. The iris uses the same blues as the staff gem, so this
    alone is not enough to keep a pixel; the neighbourhood test does that."""
    if a < 0.55:
        return False
    hue, s, v = hsv_of(r, g, b)
    return 185.0 <= hue <= 235.0 and s >= 0.42 and v >= 0.45


def is_count_blue(r, g, b, a):
    if a < 0.30:
        return False
    hue, s, v = hsv_of(r, g, b)
    return 180.0 <= hue <= 250.0 and s >= 0.35 and v >= 0.35


def is_blueish(r, g, b, a):
    if a < 0.25:
        return False
    hue, s, v = hsv_of(r, g, b)
    return 170.0 <= hue <= 255.0 and s >= 0.12 and v >= 0.2


def remap_rgb(r, g, b, a):
    """Return fool-form RGB in 0-1 sRGB. Alpha is not used except as a skip."""
    if a <= 0.004:
        return r, g, b

    hue, s, v = hsv_of(r, g, b)

    # Skin, gold stars, wooden staff, peach blush: already warm. Leave them.
    warm = (hue <= 70.0 or hue >= 330.0) and s >= 0.045
    if warm and v >= 0.12:
        return r, g, b

    # True black / closed-eye lines: keep. Only blue-tinted darks get warmed.
    if v < 0.18 and not (190.0 <= hue <= 255.0 and s >= 0.25):
        return r, g, b

    cool = (160.0 <= hue <= 275.0 and s >= 0.025) or (s < 0.10 and (b - r) > 0.012 and v > 0.2)
    if not cool:
        # Neutral white cloth -> a touch of cream, so it meets the gold hair.
        if s < 0.07 and v > 0.82:
            cr, cg, cb = 0.973, 0.941, 0.847  # #f8f0d8
            k = 0.42
            return r * (1 - k) + cr * k, g * (1 - k) + cg * k, b * (1 - k) + cb * k
        return r, g, b

    # Deep blue-black shadow / outline -> warm dark brown, stay dark.
    if v < 0.22:
        return from_hsv(22.0, min(0.55, 0.22 + s * 0.35), v * 0.92)

    # Glowing accents: orb, sparkles, gem. Sun gold #f8c830.
    if s >= 0.55 and v >= 0.70 and 185.0 <= hue <= 240.0:
        val = 0.78 + 0.20 * v
        return from_hsv(46.0, 0.78, min(1.0, val))

    # Icy hair and blue-white cloth: cream tips, champagne midtones.
    if s < 0.18:
        # Brighter pixels stay closer to cream-white; shadows pick up gold.
        shadow = max(0.0, min(1.0, (0.96 - v) / 0.55))
        sat = 0.07 + 0.30 * shadow + s * 0.35
        sat = min(0.40, sat)
        hue_out = 38.0 + 6.0 * (1.0 - shadow)
        return from_hsv(hue_out, sat, min(1.0, v * 0.99 + 0.01))

    # Ribbons, hat brim, boots, skirt blues -> pale gold / antique gold.
    shadow = max(0.0, min(1.0, 1.0 - v))
    hue_out = 34.0 + 10.0 * v
    sat = 0.22 + 0.40 * min(1.0, s) 
    sat = min(0.62, sat + 0.08 * shadow)
    # Keep the fold contrast, lift the navy a little so it reads as gold cloth
    # rather than a leftover blue shadow.
    val = min(1.0, v * 0.78 + 0.16)
    return from_hsv(hue_out, sat, val)


def _sample(px, x, y, w, h):
    if x < 0 or y < 0 or x >= w or y >= h:
        return 0.0, 0.0, 0.0, 0.0
    r, g, b, a = px[x, y]
    return r / 255.0, g / 255.0, b / 255.0, a / 255.0


def eye_guard_mask(im):
    """Keep blue pixels that sit in peach skin (the eyes).

    The staff gem and the hat jewel are the same azure, but they are surrounded
    by metal, ribbon, or empty space rather than a face, so a small neighbourhood
    count separates them. Radius and thresholds scale with sprite height so the
    same rule fits both the 216px game frames and the tall reference portrait.
    """
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    scale = h / REF_HEIGHT
    radius = max(4, int(round(EYE_RADIUS_PX * scale)))
    # 13x13 window on a game frame (radius 6). Thresholds follow the window area.
    area_scale = (radius / EYE_RADIUS_PX) ** 2
    skin_need = 28.0 * area_scale

    core = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            r, g, b, a = _sample(px, x, y, w, h)
            if not is_eye_core(r, g, b, a):
                continue
            skin = 0
            blue = 0
            y0 = max(0, y - radius)
            y1 = min(h - 1, y + radius)
            x0 = max(0, x - radius)
            x1 = min(w - 1, x + radius)
            for yy in range(y0, y1 + 1):
                for xx in range(x0, x1 + 1):
                    sr, sg, sb, sa = _sample(px, xx, yy, w, h)
                    if is_skin(sr, sg, sb, sa):
                        skin += 1
                    if is_count_blue(sr, sg, sb, sa):
                        blue += 1
            if skin >= skin_need and skin >= blue * 0.90:
                core[y][x] = True

    guard = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if not core[y][x]:
                continue
            y0 = max(0, y - 1)
            y1 = min(h - 1, y + 1)
            x0 = max(0, x - 1)
            x1 = min(w - 1, x + 1)
            for yy in range(y0, y1 + 1):
                grow = guard[yy]
                for xx in range(x0, x1 + 1):
                    sr, sg, sb, sa = _sample(px, xx, yy, w, h)
                    if is_blueish(sr, sg, sb, sa) or is_eye_core(sr, sg, sb, sa):
                        grow[xx] = True
    return guard


def apply_form(im, fool=True):
    src = im.convert("RGBA")
    w, h = src.size
    px = src.load()
    out = Image.new("RGBA", (w, h))
    op = out.load()
    guard = eye_guard_mask(src) if fool else None
    for y in range(h):
        gy = guard[y] if guard is not None else None
        for x in range(w):
            r, g, b, a = px[x, y]
            rf, gf, bf, af = r / 255.0, g / 255.0, b / 255.0, a / 255.0
            if fool and gy is not None and gy[x]:
                nr, ng, nb = rf, gf, bf
            elif fool:
                nr, ng, nb = remap_rgb(rf, gf, bf, af)
            else:
                nr, ng, nb = rf, gf, bf
            op[x, y] = (
                max(0, min(255, int(round(nr * 255)))),
                max(0, min(255, int(round(ng * 255)))),
                max(0, min(255, int(round(nb * 255)))),
                a,
            )
    return out


def knock_out_white_background(im, thresh=246):
    """Flood-fill near-white from the edges so a studio background stays white."""
    src = im.convert("RGBA")
    w, h = src.size
    px = src.load()
    seen = bytearray(w * h)
    stack = []
    for x in range(w):
        stack.append((x, 0))
        stack.append((x, h - 1))
    for y in range(h):
        stack.append((0, y))
        stack.append((w - 1, y))
    bg = [[False] * w for _ in range(h)]
    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= w or y >= h:
            continue
        i = y * w + x
        if seen[i]:
            continue
        seen[i] = 1
        r, g, b, a = px[x, y]
        if a < 250:
            continue
        if r < thresh or g < thresh or b < thresh:
            continue
        if max(r, g, b) - min(r, g, b) > 14:
            continue
        bg[y][x] = True
        stack.append((x + 1, y))
        stack.append((x - 1, y))
        stack.append((x, y + 1))
        stack.append((x, y - 1))
    return bg


def composite(im, bg=(32, 32, 38)):
    im = im.convert("RGBA")
    canvas = Image.new("RGBA", im.size, (*bg, 255))
    canvas.alpha_composite(im)
    return canvas.convert("RGB")


def side_by_side(left, right, scale=1, pad=8, bg=(32, 32, 38)):
    left = composite(left, bg)
    right = composite(right, bg)
    if scale != 1:
        left = left.resize((left.width * scale, left.height * scale), Image.NEAREST)
        right = right.resize((right.width * scale, right.height * scale), Image.NEAREST)
    h = max(left.height, right.height)
    canvas = Image.new("RGB", (left.width + right.width + pad, h), bg)
    canvas.paste(left, (0, 0))
    canvas.paste(right, (left.width + pad, 0))
    return canvas


def _check_known_colors():
    # 这些结果要和着色器对得上。改规则时两边一起改。
    expect = {
        "0c62db": "f3c735",
        "18a2fd": "facc37",
        "f2f2f9": "f9f4e5",
        "6080d0": "cbb070",
        "083088": "937438",
        "ffffff": "fcf9ef",
        "fceee9": "fceee9",
    }
    for src, dst in expect.items():
        r = int(src[0:2], 16) / 255.0
        g = int(src[2:4], 16) / 255.0
        b = int(src[4:6], 16) / 255.0
        nr, ng, nb = remap_rgb(r, g, b, 1.0)
        got = "{:02x}{:02x}{:02x}".format(int(round(nr * 255)), int(round(ng * 255)), int(round(nb * 255)))
        if got != dst:
            raise SystemExit("palette mismatch #%s -> #%s (expected #%s)" % (src, got, dst))


def _sheet(original, fool, scale, bg):
    return side_by_side(original, fool, scale=scale, bg=bg)


def main():
    import argparse
    import os

    parser = argparse.ArgumentParser(description="Render Original vs Fool palette previews.")
    parser.add_argument("--frames", default="Assets/Resources/Art/Witch/Anim")
    parser.add_argument("--out", default="/opt/cursor/artifacts")
    parser.add_argument("--original", default="")
    parser.add_argument("--fool-ref", default="")
    args = parser.parse_args()

    _check_known_colors()
    os.makedirs(args.out, exist_ok=True)

    def frame(name):
        return Image.open(os.path.join(args.frames, name)).convert("RGBA")

    stills = ("idle_0.png", "walk_0.png", "run_0.png", "jump_0.png")
    for name in stills:
        original = frame(name)
        fool = apply_form(original, True)
        sheet = _sheet(original, fool, 3, (32, 32, 38))
        out = os.path.join(args.out, "fool_" + name.replace(".png", "_compare.png"))
        sheet.save(out)
        print(out)

    def gif(prefix, names, duration_ms):
        images = []
        for name in names:
            original = frame(name)
            images.append(_sheet(original, apply_form(original, True), 2, (32, 32, 38)))
        path = os.path.join(args.out, prefix + ".gif")
        images[0].save(path, save_all=True, append_images=images[1:], duration=duration_ms, loop=0, disposal=2)
        print(path)

    gif("fool_idle", ["idle_%d.png" % i for i in range(4)], 200)
    gif("fool_run", ["run_%d.png" % i for i in range(6)], 71)

    if args.original:
        portrait = Image.open(args.original).convert("RGBA")
        bg = knock_out_white_background(portrait)
        cut = portrait.copy()
        pix = cut.load()
        for y in range(cut.height):
            row = bg[y]
            for x in range(cut.width):
                if row[x]:
                    r, g, b, a = pix[x, y]
                    pix[x, y] = (r, g, b, 0)
        fool = apply_form(cut, True)

        def on_white(im):
            canvas = Image.new("RGBA", im.size, (255, 255, 255, 255))
            canvas.alpha_composite(im.convert("RGBA"))
            return canvas

        sheet = _sheet(on_white(cut), on_white(fool), 1, (255, 255, 255))
        # 原图很高，缩一半方便看。最近邻，不把像素涂糊。
        view = sheet.resize((sheet.width // 2, sheet.height // 2), Image.NEAREST)
        out = os.path.join(args.out, "fool_portrait_compare.png")
        view.save(out)
        print(out)

    if args.fool_ref:
        ref = Image.open(args.fool_ref).convert("RGBA")
        ref_path = os.path.join(args.out, "fool_reference.png")
        composite(ref, (0, 0, 0)).save(ref_path)
        print(ref_path)


if __name__ == "__main__":
    main()
