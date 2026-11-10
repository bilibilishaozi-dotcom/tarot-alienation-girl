#!/usr/bin/env python3
"""GIF 切成游戏用的帧。

原版帧已经在 Assets/Resources/Art/Witch/Anim，这个脚本不覆盖它们。
GIF 放在 Tools/ArtIncoming/初始形态/。--check-original 只核对文件还在、帧数没变。

愚者、战车、力量的待机和奔跑会写到
Assets/Resources/Art/Witch/Forms/<Form>/idle_*.png 和 run_*.png。
每个片段（待机、奔跑、起跳、下落）共用一个裁切框和同一个偏移，
不按单帧的着地脚去挪。裁切框是所有帧包围盒的并集，水平用整段平均重心，
纵向用整段同一条脚底。脚底到轴心仍是 108 像素，和原版待机一致。
力量的下落文件叫「落下」，另外两个形态叫「落地」，都切成 fall。
人物缩放用原版待机那一档（源图内容高 227 → 画面高 205）。

用法（仓库根目录）：
  python3 Tools/build_clip.py --check-original
  python3 Tools/build_clip.py --forms
"""

import json
import uuid
from pathlib import Path

import numpy as np
from PIL import Image, ImageSequence

ROOT = Path(__file__).resolve().parents[1]
INCOMING = ROOT / "Tools" / "ArtIncoming"
ORIGINAL_DIR = INCOMING / "初始形态"
FORM_OUT = ROOT / "Assets" / "Resources" / "Art" / "Witch" / "Forms"

# 原版 idle_0：内容高 205，源内容高 227。三个形态用同一倍率，换形态时人不会突然变大。
SCALE = 205 / 227
# 原版画布 216、轴心 0.5 时，脚底在轴心正下方 108 像素。
FOOT_BELOW_PIVOT = 108
# 原版待机四帧不透明像素的水平重心，平均在轴心右边 6.35 像素。
# 形态的整段动画对准这个点，换形态时身体不横跳。
CENTROID_OFFSET = 6.35
PPU = 97
MIN_PART = 24

# 初始形态/ 里的 GIF。帧数和已经导入的 Anim 一致。攻击重砸那张朝西，当初是水平翻转后才入库的。
ORIGINAL_CLIPS = (
    ("idle", "Idle_breathing-idle_south-east.gif", 4, False),
    ("walk", "Idle_v3_walking_east.gif", 8, False),
    ("run", "Idle_running-6-frames_south-east.gif", 6, False),
    ("jump", "Idle_two-footed-jump_south-east.gif", 7, False),
    ("dash", "Idle_v3_full_sprinting_east.gif", 8, False),
    ("attack1", "Idle_custom-Action_staff_attack_horizont_east.gif", 9, False),
    ("attack2", "Idle_custom-Action_staff_attack_upward_d_east.gif", 9, False),
    ("attack3", "Idle_custom-Action_staff_attack_heavy_ov_west.gif", 9, True),
    ("airattack", "Jumping_staff_attack_custom-Jumping_staff_attack._She_is_a_south.gif", 9, False),
    ("hurt", "Idle_custom-Hit_react._A_small_impact_from_east.gif", 9, False),
    ("wallslide", "Wall_slide._Her_back_custom-Wall_slide._Her_back_is_toward_south-west.gif", 9, False),
    ("walljump", "Wall_slide._Her_back_custom-Wall_jump._She_pushes_off_a_wa_south.gif", 9, False),
)

FORMS = {
    "Fool": "愚者",
    "Chariot": "战车",
    "Strength": "力量",
}


def gif_frames(path):
    image = Image.open(path)
    frames = []
    for frame in ImageSequence.Iterator(image):
        frames.append(frame.convert("RGBA"))
    return frames


def composite_gif(path):
    """按每帧的 disposal 合成。2 = 画完后清回透明，不把上一帧叠上来。"""
    image = Image.open(path)
    canvas = Image.new("RGBA", image.size, (0, 0, 0, 0))
    composed = []
    for index in range(image.n_frames):
        image.seek(index)
        disposal = int(getattr(image, "disposal_method", 0) or 0)
        frame = image.convert("RGBA")
        shown = canvas.copy()
        shown.alpha_composite(frame)
        composed.append(shown)
        if disposal == 2:
            canvas = Image.new("RGBA", image.size, (0, 0, 0, 0))
        elif disposal != 3:
            canvas = shown
    return composed


def key_background(rgba):
    rgb = rgba[:, :, :3].astype(np.int16)
    alpha = rgba[:, :, 3]
    red = rgb[:, :, 0]
    green = rgb[:, :, 1]
    blue = rgb[:, :, 2]
    transparent = alpha < 16
    green_screen = (green > 150) & (green > red + 40) & (green > blue + 40)
    magenta = (red > 170) & (blue > 170) & (green < 90) & (np.abs(red - blue) < 80)
    out = rgba.copy()
    out[transparent | green_screen | magenta, 3] = 0
    return out


def keep_parts(mask):
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    keep = np.zeros_like(mask, dtype=bool)
    ys, xs = np.where(mask)
    for y, x in zip(ys, xs):
        if seen[y, x]:
            continue
        stack = [(int(y), int(x))]
        seen[y, x] = True
        pixels = []
        while stack:
            cy, cx = stack.pop()
            pixels.append((cy, cx))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny = cy + dy
                nx = cx + dx
                if ny < 0 or nx < 0 or ny >= height or nx >= width:
                    continue
                if not mask[ny, nx] or seen[ny, nx]:
                    continue
                seen[ny, nx] = True
                stack.append((ny, nx))
        if len(pixels) >= MIN_PART:
            for py, px in pixels:
                keep[py, px] = True
    return keep


def prepare(frame, scale=SCALE):
    keyed = key_background(np.array(frame))
    keep = keep_parts(keyed[:, :, 3] > 20)
    keyed[:, :, 3] = np.where(keep, keyed[:, :, 3], 0)
    image = Image.fromarray(keyed, "RGBA")
    size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    return np.array(image.resize(size, Image.Resampling.NEAREST))


def foot_anchor(rgba):
    """靴子所在的行，不把伸到脚前面的长枪当成脚底。"""
    mask = rgba[:, :, 3] > 20
    ys, xs = np.where(mask)
    if len(xs) == 0:
        raise RuntimeError("空帧")
    top = int(ys.min())
    bottom = int(ys.max())
    height = bottom - top + 1
    band_top = top + int(height * 0.35)
    band_bottom = top + int(height * 0.65)
    band = mask[band_top:band_bottom]
    _, body_xs = np.where(band)
    body_x = int(np.median(body_xs)) if len(body_xs) else int(np.median(xs))
    window = 40
    foot_y = bottom
    for y in range(rgba.shape[0] - 1, -1, -1):
        cols = np.where(mask[y])[0]
        cols = cols[(cols >= body_x - window) & (cols <= body_x + window)]
        if len(cols) >= 8:
            foot_y = int(y)
            break
    y0 = max(0, foot_y - 14)
    region = np.zeros_like(mask)
    region[y0:foot_y + 1, max(0, body_x - window):body_x + window + 1] = True
    _, leg_xs = np.where(mask & region)
    leg_x = float(leg_xs.mean()) if len(leg_xs) else float(body_x)
    return leg_x, foot_y


def check_original():
    missing = []
    for name, filename, count, flipped in ORIGINAL_CLIPS:
        path = ORIGINAL_DIR / filename
        if not path.exists():
            missing.append(str(path))
            continue
        frames = gif_frames(path)
        mark = "，入库时水平翻转" if flipped else ""
        print(f"{name}: {len(frames)} 帧（记录 {count}）{path.name}{mark}")
        if len(frames) != count:
            missing.append(f"{name} 帧数 {len(frames)} != {count}")
    jump = ORIGINAL_DIR / "Idle_two-footed-jump_south-east.gif"
    if jump.exists():
        total = len(gif_frames(jump))
        print(f"jump/fall 共用跳跃 GIF，前 4 帧 jump、后 {total - 4} 帧 fall")
    if missing:
        raise SystemExit("初始形态素材对不上：\n" + "\n".join(missing))
    print("初始形态路径可用。已导入的 Anim 帧不会重写。")


def place_clip(frames, scale=SCALE):
    """一个片段共用一个裁切框和同一个偏移，帧与帧的相对位置跟 GIF 一样。"""
    images = [prepare(frame, scale) for frame in frames]
    boxes = []
    centers = []
    feet = []
    for image in images:
        mask = image[:, :, 3] > 20
        ys, xs = np.where(mask)
        boxes.append((int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())))
        centers.append(float(xs.mean()))
        _, foot_y = foot_anchor(image)
        feet.append(int(foot_y))

    x0 = min(box[0] for box in boxes)
    y0 = min(box[1] for box in boxes)
    x1 = max(box[2] for box in boxes)
    y1 = max(box[3] for box in boxes)
    avg_cx = float(np.mean(centers))
    # 整段用同一条脚底。取各帧靴底的中位数，不被某一帧的长枪拉下去。
    foot_y = int(round(float(np.median(feet))))
    crop_w = x1 - x0 + 1
    crop_h = y1 - y0 + 1
    cent_in_crop = avg_cx - x0
    pad = 2
    above = (foot_y - y0) + pad
    below = (y1 - foot_y) + pad
    height = above + below + 1
    foot_row = above
    left = cent_in_crop + pad
    right = (crop_w - cent_in_crop) + pad
    width = int(np.ceil(max(2 * (left - CENTROID_OFFSET), 2 * (right + CENTROID_OFFSET))))
    if width % 2 == 0:
        width += 1
    anchor_x = width / 2.0 + CENTROID_OFFSET
    dst_x = int(round(anchor_x - cent_in_crop))
    dst_y = int(foot_row - (foot_y - y0))
    if dst_x < 0 or dst_y < 0 or dst_x + crop_w > width or dst_y + crop_h > height:
        raise RuntimeError(f"裁切框放不进画布 dst=({dst_x},{dst_y}) crop={crop_w}x{crop_h} canvas={width}x{height}")

    sheets = []
    for image in images:
        sheet = np.zeros((height, width, 4), np.uint8)
        sheet[dst_y:dst_y + crop_h, dst_x:dst_x + crop_w] = image[y0:y1 + 1, x0:x1 + 1]
        sheets.append(sheet)
    pivot_x = 0.5
    pivot_y = (FOOT_BELOW_PIVOT + below) / float(height)
    return sheets, pivot_x, pivot_y, below, centers


def meta_text(pivot_x, pivot_y):
    custom = abs(pivot_x - 0.5) > 0.001 or abs(pivot_y - 0.5) > 0.001
    alignment = 9 if custom else 0
    guid = uuid.uuid4().hex
    sprite_id = uuid.uuid4().hex
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: {alignment}
  spritePivot: {{x: {pivot_x:.6f}, y: {pivot_y:.6f}}}
  spritePixelsToUnits: {PPU}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""", pivot_x, pivot_y


def folder_meta():
    return f"""fileFormatVersion: 2
guid: {uuid.uuid4().hex}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def form_gifs(folder):
    found = {"idle": None, "run": None, "jump": None, "fall": None}
    for path in sorted(folder.glob("*.gif")):
        lower = path.name.lower()
        stem = path.stem
        if "idle" in lower:
            found["idle"] = path
        elif "running" in lower:
            found["run"] = path
        elif stem == "跳跃":
            found["jump"] = path
        elif stem in ("落地", "落下"):
            found["fall"] = path
    missing = [name for name, path in found.items() if path is None]
    if missing:
        raise SystemExit(f"{folder} 缺少 {', '.join(missing)}")
    return found


def write_sprite_meta(png_path, pivot_x, pivot_y):
    """轴心变了就改已有 .meta 里的两行，guid 保持不变。"""
    custom = abs(pivot_x - 0.5) > 0.001 or abs(pivot_y - 0.5) > 0.001
    alignment = 9 if custom else 0
    pivot_line = f"  spritePivot: {{x: {pivot_x:.6f}, y: {pivot_y:.6f}}}"
    align_line = f"  alignment: {alignment}"
    meta_path = Path(str(png_path) + ".meta")
    if meta_path.exists():
        lines = meta_path.read_text(encoding="utf-8").splitlines()
        replaced_align = False
        replaced_pivot = False
        for i, line in enumerate(lines):
            if line.startswith("  alignment:"):
                lines[i] = align_line
                replaced_align = True
            elif line.startswith("  spritePivot:"):
                lines[i] = pivot_line
                replaced_pivot = True
        if not replaced_align or not replaced_pivot:
            raise RuntimeError(f"{meta_path} 里没有 alignment 或 spritePivot")
        meta_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        return
    text, _, _ = meta_text(pivot_x, pivot_y)
    meta_path.write_text(text, encoding="utf-8")


def write_forms():
    FORM_OUT.mkdir(parents=True, exist_ok=True)
    forms_meta = ROOT / "Assets" / "Resources" / "Art" / "Witch" / "Forms.meta"
    if not forms_meta.exists():
        forms_meta.write_text(folder_meta(), encoding="utf-8")
    for form_id, folder_name in FORMS.items():
        found = form_gifs(INCOMING / folder_name)
        clips = {name: gif_frames(path) for name, path in found.items()}
        out_dir = FORM_OUT / form_id
        out_dir.mkdir(parents=True, exist_ok=True)
        dir_meta = Path(str(out_dir) + ".meta")
        if not dir_meta.exists():
            dir_meta.write_text(folder_meta(), encoding="utf-8")
        info_path = out_dir / "clip.json"
        if info_path.exists():
            info = json.loads(info_path.read_text(encoding="utf-8"))
        else:
            info = {
                "form": form_id,
                "pixelsPerUnit": PPU,
                "footBelowPivot": FOOT_BELOW_PIVOT,
                "centroidOffset": CENTROID_OFFSET,
            }
        for clip_name, frames in clips.items():
            # 待机和奔跑已经切好，这里不重写。跳跃、下落各用自己的整段裁切框。
            if clip_name in ("idle", "run"):
                continue
            sheets, pivot_x, pivot_y, below, centers = place_clip(frames)
            for index, sheet in enumerate(sheets):
                png_path = out_dir / f"{clip_name}_{index}.png"
                Image.fromarray(sheet, "RGBA").save(png_path)
                write_sprite_meta(png_path, pivot_x, pivot_y)
            info[clip_name] = {
                "canvas": [int(sheets[0].shape[1]), int(sheets[0].shape[0])],
                "pivot": [round(pivot_x, 6), round(pivot_y, 6)],
                "belowFeet": int(below),
                "frames": len(sheets),
            }
            span = max(centers) - min(centers)
            rounded = [round(value, 1) for value in centers]
            print(f"{form_id} {clip_name}: canvas {info[clip_name]['canvas'][0]}x{info[clip_name]['canvas'][1]} pivot {info[clip_name]['pivot']} below {below} src_cx {rounded} span {span:.2f}")
        info_path.write_text(json.dumps(info, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


def write_original_idle(gif_path):
    """只换初始形态待机。源文件另存为 idle_new.gif，不占用用户本地的 新待机.gif。"""
    source = Path(gif_path)
    frames = composite_gif(source)
    heights = []
    for frame in frames:
        mask = np.array(frame)[:, :, 3] > 20
        ys, _ = np.where(mask)
        heights.append(int(ys.max() - ys.min() + 1))
    # 现有跑步/跳跃人物高约 205。按这套新待机的内容高缩放，换动作时人不变大变小。
    target_height = 205
    source_height = int(round(float(np.median(heights))))
    scale = target_height / float(source_height)
    sheets, pivot_x, pivot_y, below, centers = place_clip(frames, scale)
    out_dir = ROOT / "Assets" / "Resources" / "Art" / "Witch" / "Anim"
    for index, sheet in enumerate(sheets):
        png_path = out_dir / f"idle_{index}.png"
        Image.fromarray(sheet, "RGBA").save(png_path)
        write_sprite_meta(png_path, pivot_x, pivot_y)
    # 旧待机只有 4 帧。多出来的帧是新文件；少掉的旧帧不留，避免播到旧图。
    for stale in out_dir.glob("idle_*.png"):
        number = stale.stem.split("_")[-1]
        if number.isdigit() and int(number) >= len(sheets):
            stale.unlink()
            meta = Path(str(stale) + ".meta")
            if meta.exists():
                meta.unlink()
    info = {
        "clip": "idle",
        "canvas": [int(sheets[0].shape[1]), int(sheets[0].shape[0])],
        "feetY": int(sheets[0].shape[0] - below),
        "frames": len(sheets),
        "pivot": [round(pivot_x, 6), round(pivot_y, 6)],
        "source": "idle_new.gif",
        "sourceFrames": len(frames),
        "sourceHeight": source_height,
        "scale": round(scale, 6),
    }
    (out_dir / "idle.json").write_text(json.dumps(info, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    saved = ORIGINAL_DIR / "idle_new.gif"
    saved.write_bytes(source.read_bytes())
    span = max(centers) - min(centers)
    print(
        f"original idle: {len(sheets)} frames canvas {info['canvas'][0]}x{info['canvas'][1]} "
        f"pivot {info['pivot']} below {below} scale {scale:.4f} from height {source_height} "
        f"cx span {span:.2f}"
    )
    return sheets, centers


def write_fool_attacks(attack1_path, attack2_path, attack3_path):
    """愚者地面普攻三段各一套。缩放跟现有愚者待机同一档，不被刀光撑大。"""
    specs = (
        ("attack1", attack1_path),
        ("attack2", attack2_path),
        ("attack3", attack3_path),
    )
    out_dir = FORM_OUT / "Fool"
    out_dir.mkdir(parents=True, exist_ok=True)
    info_path = out_dir / "clip.json"
    info = json.loads(info_path.read_text(encoding="utf-8")) if info_path.exists() else {"form": "Fool"}
    info.pop("attack12", None)
    for stale in out_dir.glob("attack12_*.png"):
        stale.unlink()
        meta = Path(str(stale) + ".meta")
        if meta.exists():
            meta.unlink()
    saved_dir = INCOMING / "愚者"
    saved_dir.mkdir(parents=True, exist_ok=True)
    for clip_name, gif_path in specs:
        source = Path(gif_path)
        frames = composite_gif(source)
        sheets, pivot_x, pivot_y, below, centers = place_clip(frames, SCALE)
        for index, sheet in enumerate(sheets):
            png_path = out_dir / f"{clip_name}_{index}.png"
            Image.fromarray(sheet, "RGBA").save(png_path)
            write_sprite_meta(png_path, pivot_x, pivot_y)
        info[clip_name] = {
            "canvas": [int(sheets[0].shape[1]), int(sheets[0].shape[0])],
            "pivot": [round(pivot_x, 6), round(pivot_y, 6)],
            "belowFeet": int(below),
            "frames": len(sheets),
            "scale": round(SCALE, 6),
        }
        saved = saved_dir / f"fool_{clip_name}.gif"
        saved.write_bytes(source.read_bytes())
        span = max(centers) - min(centers)
        print(
            f"fool {clip_name}: {len(sheets)} frames canvas {info[clip_name]['canvas'][0]}x{info[clip_name]['canvas'][1]} "
            f"pivot {info[clip_name]['pivot']} below {below} cx span {span:.2f} bytes {source.stat().st_size}"
        )
    info_path.write_text(json.dumps(info, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


def write_chariot_attacks(attack1_path, attack2_path, attack3_path):
    """战车地面普攻三段。缩放跟现有战车待机同一档，长枪和轮子不把人缩小。"""
    specs = (
        ("attack1", attack1_path),
        ("attack2", attack2_path),
        ("attack3", attack3_path),
    )
    out_dir = FORM_OUT / "Chariot"
    out_dir.mkdir(parents=True, exist_ok=True)
    info_path = out_dir / "clip.json"
    info = json.loads(info_path.read_text(encoding="utf-8")) if info_path.exists() else {"form": "Chariot"}
    saved_dir = INCOMING / "战车"
    saved_dir.mkdir(parents=True, exist_ok=True)
    for clip_name, gif_path in specs:
        source = Path(gif_path)
        frames = composite_gif(source)
        sheets, pivot_x, pivot_y, below, centers = place_clip(frames, SCALE)
        for index, sheet in enumerate(sheets):
            png_path = out_dir / f"{clip_name}_{index}.png"
            Image.fromarray(sheet, "RGBA").save(png_path)
            write_sprite_meta(png_path, pivot_x, pivot_y)
        info[clip_name] = {
            "canvas": [int(sheets[0].shape[1]), int(sheets[0].shape[0])],
            "pivot": [round(pivot_x, 6), round(pivot_y, 6)],
            "belowFeet": int(below),
            "frames": len(sheets),
            "scale": round(SCALE, 6),
        }
        saved = saved_dir / f"chariot_{clip_name}.gif"
        saved.write_bytes(source.read_bytes())
        span = max(centers) - min(centers)
        print(
            f"chariot {clip_name}: {len(sheets)} frames canvas {info[clip_name]['canvas'][0]}x{info[clip_name]['canvas'][1]} "
            f"pivot {info[clip_name]['pivot']} below {below} cx span {span:.2f} bytes {source.stat().st_size}"
        )
    info_path.write_text(json.dumps(info, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


def main():
    import sys
    args = set(sys.argv[1:])
    if not args or "--check-original" in args:
        check_original()
    if "--forms" in args or (
        not args
        and "--original-idle" not in args
        and "--fool-attack" not in args
        and "--chariot-attack" not in args
    ):
        write_forms()
    if "--original-idle" in args:
        source = ORIGINAL_DIR / "idle_new.gif"
        uploaded = Path("/home/ubuntu/.cursor/projects/workspace/uploads/new-original-idle_6606.gif")
        gif = source if source.exists() else uploaded
        if not gif.exists():
            raise SystemExit("找不到初始形态的新待机 GIF")
        write_original_idle(gif)
    if "--chariot-attack" in args:
        uploads = Path("/home/ubuntu/.cursor/projects/workspace/uploads")
        write_chariot_attacks(
            uploads / "chariot-attack1_18d3.gif",
            uploads / "chariot-attack2_0cdc.gif",
            uploads / "chariot-attack3_6d0e.gif",
        )
        return
    if "--fool-attack" in args:
        uploads = Path("/home/ubuntu/.cursor/projects/workspace/uploads")
        write_fool_attacks(
            uploads / "fool-attack1-v2_83b0.gif",
            uploads / "fool-attack2-v2_f380.gif",
            uploads / "fool-attack3-v2_c17a.gif",
        )


if __name__ == "__main__":
    main()
