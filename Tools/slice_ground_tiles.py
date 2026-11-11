"""把地块大图切成单独的瓦片。换图后重跑：python3 Tools/slice_ground_tiles.py"""
import uuid
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
INCOMING = ROOT / "Tools" / "ArtIncoming" / "地块"
OUT = ROOT / "Assets" / "Resources" / "Art" / "Tiles"
# 和 FirstLevel.GroundTilePixelsPerUnit 一致：32 像素 = 1 个世界单位。
PIXELS_PER_UNIT = 32

# 131 的大图，4x4，格距 33，格间 1 像素缝。
CELL = 33
COLS = 4
ROWS = 4


def crop_grid(image, y_start, height):
    tiles = []
    for row in range(ROWS):
        for col in range(COLS):
            x0 = col * CELL
            y0 = row * CELL + y_start
            tiles.append(image.crop((x0, y0, x0 + 32, y0 + height)))
    return tiles


def meta_text(pivot_x, pivot_y):
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
  alignment: 9
  spritePivot: {{x: {pivot_x:.6f}, y: {pivot_y:.6f}}}
  spritePixelsToUnits: {PIXELS_PER_UNIT}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
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
"""


def clear_black_frame(tile):
    """去掉贴着四边的 1 像素黑框，不动内部的深色砖缝。"""
    arr = np.array(tile.convert("RGBA"))
    dark = arr[:, :, :3].max(axis=2) < 22
    arr[0, dark[0], 3] = 0
    arr[-1, dark[-1], 3] = 0
    arr[dark[:, 0], 0, 3] = 0
    arr[dark[:, -1], -1, 3] = 0
    return Image.fromarray(arr, "RGBA")


def clear_edge_checker(tile):
    """从边缘向内清掉近白、浅灰的假棋盘格。柱身的灰更暗，连不到边缘就不会被清掉。"""
    arr = np.array(tile.convert("RGBA"))
    rgb = arr[:, :, :3].astype(np.int16)
    luma = rgb.sum(axis=2) / 3.0
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    fake = (luma >= 210) & (sat <= 20) & (arr[:, :, 3] > 0)
    height, width = fake.shape
    seen = np.zeros_like(fake)
    queue = deque()
    for x in range(width):
        for y in (0, height - 1):
            if fake[y, x]:
                seen[y, x] = True
                queue.append((y, x))
    for y in range(height):
        for x in (0, width - 1):
            if fake[y, x] and not seen[y, x]:
                seen[y, x] = True
                queue.append((y, x))
    while queue:
        y, x = queue.popleft()
        arr[y, x, 3] = 0
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if ny < 0 or nx < 0 or ny >= height or nx >= width or seen[ny, nx] or not fake[ny, nx]:
                continue
            seen[ny, nx] = True
            queue.append((ny, nx))
    return Image.fromarray(arr, "RGBA")


def pad_bottom(tile, height):
    if tile.height == height:
        return tile
    canvas = Image.new("RGBA", (tile.width, height), (0, 0, 0, 0))
    canvas.paste(tile, (0, 0))
    return canvas


def write_tiles(prefix, tiles):
    for index, tile in enumerate(tiles):
        path = OUT / f"{prefix}_{index}.png"
        tile.save(path)
        meta = Path(str(path) + ".meta")
        if not meta.exists():
            # 轴心在左下角，和 FirstLevel 里从左下往右上铺的算法一致。
            meta.write_text(meta_text(0.0, 0.0), encoding="utf-8")
        print(f"{prefix}_{index}: {tile.size[0]}x{tile.size[1]}")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    folder_meta = Path(str(OUT) + ".meta")
    if not folder_meta.exists():
        folder_meta.write_text(
            "fileFormatVersion: 2\n"
            f"guid: {uuid.uuid4().hex}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n",
            encoding="utf-8",
        )
    stone = Image.open(INCOMING / "stone_top.png").convert("RGBA")
    dirt = Image.open(INCOMING / "dirt_fill.png").convert("RGBA")
    # 石壁：每格 y = r*33+1 .. r*33+29，高 29。
    write_tiles("stone", crop_grid(stone, 1, 29))
    # 泥土：丢掉每格顶部 6 行亮带，从 y = r*33+7 取到 r*33+29，高 23。
    write_tiles("dirt", crop_grid(dirt, 7, 23))
    import_extra_sheets()


# 墙面 A 里这几格角落有白色假透明，整格不用。
WALL_A_SKIP = {(0, 1), (0, 2), (0, 3), (2, 3)}


def import_extra_sheets():
    wall_a = Image.open(INCOMING / "wall_face_a.png").convert("RGBA")
    wall_b = Image.open(INCOMING / "wall_face_b.png").convert("RGBA")
    walls = []
    for row, col, tile in indexed(crop_grid(wall_b, 1, 29)):
        walls.append(clear_black_frame(tile))
    for row, col, tile in indexed(crop_grid(wall_a, 1, 29)):
        if (row, col) in WALL_A_SKIP:
            print(f"skip wall_a r{row}c{col}: white corner")
            continue
        walls.append(clear_black_frame(tile))
    write_tiles("wall", walls)

    markers = Image.open(INCOMING / "markers.png").convert("RGBA")
    groups = {"seal": [], "pillar": [], "gold": [], "rail": [], "lip": []}
    keys = ("seal", "pillar", "gold", "split")
    for row, col, tile in indexed(crop_grid(markers, 3, 25)):
        cleaned = clear_edge_checker(clear_black_frame(tile))
        if keys[row] != "split":
            groups[keys[row]].append(cleaned)
            continue
        # 上半是灰栏杆，下半是砖红边沿。从第 12 行分开。
        groups["rail"].append(cleaned.crop((0, 0, cleaned.width, 12)))
        groups["lip"].append(cleaned.crop((0, 12, cleaned.width, cleaned.height)))
    for name, tiles in groups.items():
        write_tiles(name, tiles)

    ground = Image.open(INCOMING / "ground_variants_32x22.png").convert("RGBA")
    # 这是整格地面，不是左右收边。垫成 23 高，才能和泥土行对齐。
    extras = [pad_bottom(clear_black_frame(tile), 23) for _, _, tile in indexed(crop_grid(ground, 5, 22))]
    write_tiles("ground", extras)
    print("thin_ledge.png 只有 3 像素高，第 1、3 行还是假透明，不导入。")


def indexed(tiles):
    for index, tile in enumerate(tiles):
        yield divmod(index, COLS)[0], divmod(index, COLS)[1], tile


if __name__ == "__main__":
    main()
