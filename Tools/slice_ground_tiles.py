"""把地块大图切成单独的瓦片。换图后重跑：python3 Tools/slice_ground_tiles.py"""
import uuid
from pathlib import Path

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


if __name__ == "__main__":
    main()
