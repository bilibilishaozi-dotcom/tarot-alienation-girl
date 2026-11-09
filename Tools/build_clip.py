#!/usr/bin/env python3
"""GIF 切成游戏用的帧。

原版帧已经在 Assets/Resources/Art/Witch/Anim，这个脚本不覆盖它们。
GIF 放在 Tools/ArtIncoming/初始形态/。--check-original 只核对文件还在、帧数没变。

愚者、战车、力量的待机和奔跑会写到
Assets/Resources/Art/Witch/Forms/<Form>/idle_*.png 和 run_*.png。
每个片段（一个形态的待机，或一个形态的奔跑）共用一个裁切框和同一个偏移，
不按单帧的着地脚去挪。裁切框是所有帧包围盒的并集，水平用整段平均重心，
纵向用整段同一条脚底。脚到底到轴心仍是 108 像素，和原版待机一致。
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


def prepare(frame):
    keyed = key_background(np.array(frame))
    keep = keep_parts(keyed[:, :, 3] > 20)
    keyed[:, :, 3] = np.where(keep, keyed[:, :, 3], 0)
    image = Image.fromarray(keyed, "RGBA")
    size = (max(1, round(image.width * SCALE)), max(1, round(image.height * SCALE)))
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


def place_clip(frames):
    """一个片段共用一个裁切框和同一个偏移，帧与帧的相对位置跟 GIF 一样。"""
    images = [prepare(frame) for frame in frames]
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
    idle = None
    run = None
    for path in sorted(folder.glob("*.gif")):
        lower = path.name.lower()
        if "idle" in lower:
            idle = path
        elif "running" in lower:
            run = path
    if idle is None or run is None:
        raise SystemExit(f"{folder} 里要有 idle 和 running 各一份 GIF")
    return idle, run


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
        idle_path, run_path = form_gifs(INCOMING / folder_name)
        clips = {
            "idle": gif_frames(idle_path),
            "run": gif_frames(run_path),
        }
        out_dir = FORM_OUT / form_id
        out_dir.mkdir(parents=True, exist_ok=True)
        dir_meta = Path(str(out_dir) + ".meta")
        if not dir_meta.exists():
            dir_meta.write_text(folder_meta(), encoding="utf-8")
        info = {
            "form": form_id,
            "pixelsPerUnit": PPU,
            "footBelowPivot": FOOT_BELOW_PIVOT,
            "centroidOffset": CENTROID_OFFSET,
        }
        for clip_name, frames in clips.items():
            # 待机、奔跑各用自己的裁切框，框内各帧不再单独挪。
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
        (out_dir / "clip.json").write_text(json.dumps(info, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


def main():
    import sys
    args = set(sys.argv[1:])
    if not args or "--check-original" in args:
        check_original()
    if "--forms" in args or not args:
        write_forms()


if __name__ == "__main__":
    main()
