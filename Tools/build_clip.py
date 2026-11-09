#!/usr/bin/env python3
"""GIF 切成游戏用的帧。

原版帧已经在 Assets/Resources/Art/Witch/Anim，这个脚本不覆盖它们。
GIF 放在 Tools/ArtIncoming/初始形态/。--check-original 只核对文件还在、帧数没变。

愚者、战车、力量的待机和奔跑会写到
Assets/Resources/Art/Witch/Forms/<Form>/idle_*.png 和 run_*.png。
画布按脚底对齐：脚到轴心的距离和原版待机一致（216 高、轴心在正中时是 108 像素），
人物缩放用原版待机那一档（源图内容高 227 → 画面高 205）。
长枪可以画出脚底线，不会把靴子抬起来。

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
# 原版 idle_0 腿的重心在轴心右边约 12.5 像素。三个形态对准同一个点。
LEG_OFFSET = 12.5
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


def place_form(frames):
    prepared = []
    for frame in frames:
        image = prepare(frame)
        leg_x, foot_y = foot_anchor(image)
        prepared.append((image, leg_x, foot_y))

    min_x = min_y = 10 ** 9
    max_x = max_y = -10 ** 9
    for image, leg_x, foot_y in prepared:
        mask = image[:, :, 3] > 20
        ys, xs = np.where(mask)
        rel_x = xs.astype(np.float64) - leg_x
        rel_y = ys.astype(np.float64) - foot_y
        min_x = min(min_x, float(rel_x.min()))
        max_x = max(max_x, float(rel_x.max()))
        min_y = min(min_y, float(rel_y.min()))
        max_y = max(max_y, float(rel_y.max()))

    pad = 2
    need_left = int(np.ceil(-min_x)) + pad
    need_right = int(np.ceil(max_x)) + pad
    above = int(np.ceil(-min_y)) + pad
    below = int(np.ceil(max_y)) + pad
    width = max(int(np.ceil(2 * (need_left - LEG_OFFSET))), int(np.ceil(2 * (need_right + LEG_OFFSET))))
    width = max(width, need_left + need_right + 1)
    if width % 2 == 0:
        width += 1
    anchor_x = width / 2 + LEG_OFFSET
    if anchor_x < need_left or width - anchor_x < need_right:
        width += 4
        anchor_x = width / 2 + LEG_OFFSET
    height = above + below + 1
    foot_row = above
    sheets = []
    for image, leg_x, foot_y in prepared:
        sheet = np.zeros((height, width, 4), np.uint8)
        offset_x = int(round(anchor_x - leg_x))
        offset_y = int(round(foot_row - foot_y))
        src_y0 = max(0, -offset_y)
        src_x0 = max(0, -offset_x)
        dst_y = max(0, offset_y)
        dst_x = max(0, offset_x)
        copy_h = min(image.shape[0] - src_y0, height - dst_y)
        copy_w = min(image.shape[1] - src_x0, width - dst_x)
        if copy_h > 0 and copy_w > 0:
            sheet[dst_y:dst_y + copy_h, dst_x:dst_x + copy_w] = image[src_y0:src_y0 + copy_h, src_x0:src_x0 + copy_w]
        sheets.append(sheet)
    pivot_x = 0.5
    pivot_y = (FOOT_BELOW_PIVOT + below) / height
    return sheets, pivot_x, pivot_y, below


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
        # 待机和奔跑共用一块画布、同一个轴心，两段动画切换时人不跳。
        combined = clips["idle"] + clips["run"]
        sheets, pivot_x, pivot_y, below = place_form(combined)
        idle_count = len(clips["idle"])
        out_dir = FORM_OUT / form_id
        out_dir.mkdir(parents=True, exist_ok=True)
        dir_meta = Path(str(out_dir) + ".meta")
        if not dir_meta.exists():
            dir_meta.write_text(folder_meta(), encoding="utf-8")
        names = [f"idle_{i}" for i in range(idle_count)] + [f"run_{i}" for i in range(len(clips["run"]))]
        for name, sheet in zip(names, sheets):
            png_path = out_dir / f"{name}.png"
            Image.fromarray(sheet, "RGBA").save(png_path)
            meta, _, _ = meta_text(pivot_x, pivot_y)
            (out_dir / f"{name}.png.meta").write_text(meta, encoding="utf-8")
        info = {
            "form": form_id,
            "canvas": [int(sheets[0].shape[1]), int(sheets[0].shape[0])],
            "pivot": [round(pivot_x, 6), round(pivot_y, 6)],
            "pixelsPerUnit": PPU,
            "filter": "Point",
            "belowFeet": int(below),
            "idleFrames": idle_count,
            "runFrames": len(clips["run"]),
            "frameMs": 200,
        }
        (out_dir / "clip.json").write_text(json.dumps(info, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
        print(f"{form_id}: canvas {info['canvas'][0]}x{info['canvas'][1]} pivot {info['pivot']} idle {idle_count} run {info['runFrames']} below {below}")


def main():
    import sys
    args = set(sys.argv[1:])
    if not args or "--check-original" in args:
        check_original()
    if "--forms" in args or not args:
        write_forms()


if __name__ == "__main__":
    main()
