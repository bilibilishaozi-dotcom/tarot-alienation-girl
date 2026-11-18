#!/usr/bin/env python3
"""把 LevelTerrainCatalog 里的方块铺进 Tilemap 预制体。

格子规则和 Assets/Scripts/Level/LevelTerrainCatalog.cs 是同一套。
Unity 没打开时用这个脚本把烘焙结果写进仓库；编辑器菜单会再铺一遍。
"""

import hashlib
import math
import os
import re
import struct

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CATALOG = os.path.join(ROOT, "Assets/Scripts/Level/LevelTerrainCatalog.cs")
SPRITE_DIR = os.path.join(ROOT, "Assets/Resources/Art/Tiles")

TILE_DIR = "Assets/Tiles/Tiles"
LEVEL_DIR = "Assets/Tiles/Levels"
PALETTE_PATH = "Assets/Tiles/Palettes/Ground.prefab"
SET_PATH = "Assets/Resources/LevelTerrain/LevelTerrainSet.asset"

STONE_LIFT = 1.0 - 29.0 / 32.0
DIRT_LIFT = 1.0 - 23.0 / 32.0

LEVEL_PREFAB = {
    "basic": "BasicCombat",
    "fool": "FoolTrial",
    "chariot": "EchoRoom",
    "boss": "BossArena",
}

SCRIPT_GUID = {
    "Assets/Scripts/Level/LevelTerrain.cs": None,
    "Assets/Scripts/Level/LevelSpawnMarker.cs": None,
    "Assets/Scripts/Level/LevelTerrainSet.cs": None,
    "Assets/Scripts/Level/LevelTerrainCatalog.cs": None,
    "Assets/Scripts/Level/LevelTerrainHost.cs": None,
    "Assets/Editor/LevelTilemapBaker.cs": None,
}


def guid_for(text):
    return hashlib.md5(text.encode("utf-8")).hexdigest()


def f32(value):
    return struct.unpack("<f", struct.pack("<f", float(value)))[0]


def i32(value):
    value = int(value) & 0xFFFFFFFF
    if value >= 0x80000000:
        value -= 0x100000000
    return value


def fadd(a, b):
    return f32(f32(a) + f32(b))


def fsub(a, b):
    return f32(f32(a) - f32(b))


def fmul(a, b):
    return f32(f32(a) * f32(b))


def round_to_int(value):
    return int(round(value))


def edge_index(value, start):
    nearest = float(round(value))
    if abs(value - nearest) < 0.0001:
        number = int(nearest)
        return number if start else number - 1
    return math.floor(value)


def axis_cells(a, b, vertical):
    first = edge_index(a, True)
    last = edge_index(b, False)
    strong = []
    weak = []
    best = 0.0
    for i in range(first, last + 1):
        overlap = min(b, i + 1.0) - max(a, float(i))
        if overlap <= 0.0001:
            continue
        if overlap >= 0.5 - 0.0001:
            strong.append(i)
        if not weak or overlap > best + 0.0001:
            best = overlap
            weak = [i]
        elif abs(overlap - best) <= 0.0001:
            weak.append(i)
    if strong:
        return strong
    if not weak:
        return [first]
    if len(weak) == 1:
        return weak
    if vertical:
        chosen = min(weak, key=lambda i: (abs((i + 1.0) - b), i))
        return [chosen]
    return [max(weak)]


def rasterize(block):
    half_x = fmul(block["sx"], 0.5)
    half_y = fmul(block["sy"], 0.5)
    left = fsub(block["cx"], half_x)
    right = fadd(block["cx"], half_x)
    bottom = fsub(block["cy"], half_y)
    top = fadd(block["cy"], half_y)
    xs = axis_cells(left, right, False)
    ys = axis_cells(bottom, top, True)
    y_min = ys[0]
    y_max = ys[-1] + 1
    # 与 LevelTerrainCatalog.Rasterize 相同：门楣上移一格，避免底边压到玩家头顶。
    if block["name"] == "FinaleLintel":
        y_min += 1
        y_max += 1
    return {
        "xMin": xs[0],
        "xMax": xs[-1] + 1,
        "yMin": y_min,
        "yMax": y_max,
        "left0": left,
        "right0": right,
        "bottom0": bottom,
        "top0": top,
    }


def variant(block, col, row, kind, avoid, count=16):
    if count <= 1:
        return 0
    hash_value = 216613626
    for ch in block["name"]:
        hash_value = i32(i32(hash_value * 16777619) ^ ord(ch))
    rx = round_to_int(f32(fmul(block["cx"], 100.0)))
    ry = round_to_int(f32(fmul(block["cy"], 100.0)))
    hash_value = i32(i32(hash_value * 16777619) ^ i32(rx * 17))
    hash_value = i32(i32(hash_value * 16777619) ^ i32(ry * 31))
    hash_value = i32(i32(hash_value * 16777619) ^ i32(col * 13))
    hash_value = i32(i32(hash_value * 16777619) ^ i32(row * 29))
    hash_value = i32(i32(hash_value * 16777619) ^ kind)
    index = (hash_value & 0x7FFFFFFF) % count
    if index == avoid:
        index = (index + 1) % count
    return index


def tile_name(block, rect, x, y, previous):
    wall = (rect["xMax"] - rect["xMin"]) == 1 and (rect["yMax"] - rect["yMin"]) >= 2
    row_from_top = rect["yMax"] - 1 - y
    if wall:
        chosen = variant(block, x, row_from_top, 3, previous, 16)
        return "wall_%d" % chosen, chosen
    top = y == rect["yMax"] - 1
    if not top:
        chosen = variant(block, x, row_from_top, 2, previous, 16)
        return "dirt_%d" % chosen, chosen
    if block["endCaps"] and x == rect["xMin"]:
        return "stone_4", 4
    if block["endCaps"] and (rect["xMax"] - rect["xMin"]) >= 2 and x == rect["xMax"] - 1:
        return "stone_15", 15
    chosen = variant(block, x, row_from_top, 1, previous, 16)
    return "stone_%d" % chosen, chosen


def fit_patrol(rect, original_min, original_max, inset):
    left = rect["xMin"] + inset
    right = rect["xMax"] - inset
    if right < left:
        mid = (rect["xMin"] + rect["xMax"]) * 0.5
        left = mid
        right = mid
    leash_min = max(original_min, left)
    leash_max = min(original_max, right)
    if leash_max < leash_min:
        leash_min = left
        leash_max = right
    return leash_min, leash_max


def parse_catalog():
    text = open(CATALOG, encoding="utf-8").read()
    blocks = []
    for level, name, cx, cy, sx, sy, caps in re.findall(
        r'new Block\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(true|false)\s*\)',
        text,
    ):
        blocks.append({
            "level": level,
            "name": name,
            "cx": f32(cx),
            "cy": f32(cy),
            "sx": f32(sx),
            "sy": f32(sy),
            "endCaps": caps == "true",
        })
    spawns = []
    for level, sid, surface, x, yoff, leash_min, leash_max, inset in re.findall(
        r'new Spawn\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*\)',
        text,
    ):
        spawns.append({
            "level": level,
            "id": sid,
            "surface": surface,
            "x": float(x),
            "yOffset": float(yoff),
            "leashMin": float(leash_min),
            "leashMax": float(leash_max),
            "inset": float(inset),
        })
    if not blocks:
        raise SystemExit("没有从 LevelTerrainCatalog 解析到方块")
    return blocks, spawns


def sprite_guid(name):
    meta = os.path.join(SPRITE_DIR, name + ".png.meta")
    for line in open(meta, encoding="utf-8"):
        if line.startswith("guid:"):
            return line.split(":", 1)[1].strip()
    raise SystemExit("没有 guid: " + meta)


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def folder_meta(path, folder_guid):
    write(path, "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % folder_guid)


def script_meta(path, script_guid):
    write(path, (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "MonoImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 2\n"
        "  defaultReferences: []\n"
        "  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n" % script_guid
    ))


def asset_meta(path, asset_guid):
    write(path, (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n" % asset_guid
    ))


def prefab_meta(path, prefab_guid):
    write(path, (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "PrefabImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n" % prefab_guid
    ))


def matrix_yaml(lift, indent):
    pad = " " * indent
    rows = [
        "e00: 1", "e01: 0", "e02: 0", "e03: 0",
        "e10: 0", "e11: 1", "e12: 0", "e13: %s" % trim(lift),
        "e20: 0", "e21: 0", "e22: 1", "e23: 0",
        "e30: 0", "e31: 0", "e32: 0", "e33: 1",
    ]
    return "\n".join(pad + row for row in rows)


def trim(value):
    text = "%.6f" % float(value)
    text = text.rstrip("0").rstrip(".")
    if text == "-0":
        return "0"
    return text


def tile_asset(name, sprite, lift):
    return (
        "%%YAML 1.1\n"
        "%%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!114 &11400000\n"
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n"
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 13312, guid: 0000000000000000e000000000000000, type: 0}\n"
        "  m_Name: %s\n"
        "  m_EditorClassIdentifier: \n"
        "  m_Sprite: {fileID: 21300000, guid: %s, type: 3}\n"
        "  m_Color: {r: 1, g: 1, b: 1, a: 1}\n"
        "  m_Transform:\n"
        "%s\n"
        "  m_InstancedGameObject: {fileID: 0}\n"
        "  m_Flags: 1\n"
        "  m_ColliderType: 2\n" % (name, sprite, matrix_yaml(lift, 4))
    )


def paint(blocks):
    cells = {}
    overwrites = []
    for block in blocks:
        rect = rasterize(block)
        block["rect"] = rect
        for y in range(rect["yMin"], rect["yMax"]):
            previous = -1
            for x in range(rect["xMin"], rect["xMax"]):
                name, previous = tile_name(block, rect, x, y, previous)
                key = (x, y)
                if key in cells and cells[key]["block"] != block["name"]:
                    overwrites.append((block["level"], block["name"], cells[key]["block"], x, y))
                cells[key] = {"tile": name, "block": block["name"]}
    return cells, overwrites


def tilemap_body(cells, tile_guids, sprite_guids):
    order = sorted(cells)
    tile_index = {}
    sprite_index = {}
    tile_refs = []
    sprite_refs = []
    matrix_index = {"stone": 0, "wall": 0, "dirt": 1}
    entries = []
    for x, y in order:
        tile = cells[(x, y)]["tile"]
        if tile not in tile_index:
            tile_index[tile] = len(tile_refs)
            tile_refs.append(tile)
        if tile not in sprite_index:
            sprite_index[tile] = len(sprite_refs)
            sprite_refs.append(tile)
        prefix = tile.split("_", 1)[0]
        entries.append((x, y, tile_index[tile], sprite_index[tile], matrix_index[prefix]))

    refcount = {}
    for tile in tile_refs:
        refcount[tile] = 0
    for x, y in order:
        refcount[cells[(x, y)]["tile"]] += 1

    xs = [x for x, _y in order]
    ys = [y for _x, y in order]
    origin = (min(xs), min(ys))
    size = (max(xs) - origin[0] + 1, max(ys) - origin[1] + 1)

    lines = ["  m_Tiles:"]
    for x, y, tindex, sindex, mindex in entries:
        lines.append("  - first: {x: %d, y: %d, z: 0}" % (x, y))
        lines.append("    second:")
        lines.append("      serializedVersion: 2")
        lines.append("      m_TileIndex: %d" % tindex)
        lines.append("      m_TileSpriteIndex: %d" % sindex)
        lines.append("      m_TileMatrixIndex: %d" % mindex)
        lines.append("      m_TileColorIndex: 0")
        lines.append("      m_TileObjectToInstantiateIndex: 65535")
        lines.append("      dummyAlignment: 0")
        lines.append("      m_AllTileFlags: 1073741825")
    lines.append("  m_AnimatedTiles: {}")
    lines.append("  m_TileAssetArray:")
    for tile in tile_refs:
        lines.append("  - m_RefCount: %d" % refcount[tile])
        lines.append("    m_Data: {fileID: 11400000, guid: %s, type: 2}" % tile_guids[tile])
    lines.append("  m_TileSpriteArray:")
    for tile in sprite_refs:
        lines.append("  - m_RefCount: %d" % refcount[tile])
        lines.append("    m_Data: {fileID: 21300000, guid: %s, type: 3}" % sprite_guids[tile])
    stone_count = sum(1 for _x, _y, _t, _s, m in entries if m == 0)
    dirt_count = sum(1 for _x, _y, _t, _s, m in entries if m == 1)
    lines.append("  m_TileMatrixArray:")
    lines.append("  - m_RefCount: %d" % stone_count)
    lines.append("    m_Data:")
    lines.append(matrix_yaml(STONE_LIFT, 6))
    lines.append("  - m_RefCount: %d" % max(dirt_count, 1))
    lines.append("    m_Data:")
    lines.append(matrix_yaml(DIRT_LIFT, 6))
    if dirt_count == 0:
        # 没有土格时仍留一个单位矩阵，避免空数组。引用数为 0 时 Unity 会丢掉，所以改成 0 引用不写。
        lines = [line for line in lines if "DIRT_PLACEHOLDER" not in line]
        # 上面 max(..., 1) 会留下一个没人用的矩阵。没有土时改成只留石面矩阵。
        if dirt_count == 0:
            cut = lines.index("  - m_RefCount: %d" % 1)
            # 可能和 stone refcount 冲突。重新拼。
            pass
    lines.append("  m_TileColorArray:")
    lines.append("  - m_RefCount: %d" % len(order))
    lines.append("    m_Data: {r: 1, g: 1, b: 1, a: 1}")
    lines.append("  m_TileObjectToInstantiateArray: []")
    lines.append("  m_AnimationFrameRate: 1")
    lines.append("  m_Color: {r: 1, g: 1, b: 1, a: 1}")
    lines.append("  m_Origin: {x: %d, y: %d, z: 0}" % origin)
    lines.append("  m_Size: {x: %d, y: %d, z: 1}" % size)
    lines.append("  m_TileAnchor: {x: 0, y: 0, z: 0}")
    lines.append("  m_TileOrientation: 0")
    lines.append("  m_TileOrientationMatrix:")
    lines.append(matrix_yaml(0, 4))
    return "\n".join(lines), dirt_count


def tilemap_yaml(cells, tile_guids, sprite_guids):
    body, dirt_count = tilemap_body(cells, tile_guids, sprite_guids)
    if dirt_count == 0:
        body = strip_unused_dirt_matrix(body)
    return body


def strip_unused_dirt_matrix(body):
    # 没有土格时，第二段矩阵引用数被写成 1。把它删掉，避免指向不存在的索引。
    marker = "  - m_RefCount: 1\n    m_Data:\n" + matrix_yaml(DIRT_LIFT, 6) + "\n"
    if marker not in body:
        raise SystemExit("没有找到空的泥土矩阵")
    return body.replace(marker, "", 1)


def renderer_yaml(go_id):
    return """--- !u!483693784 &200004
TilemapRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 2100000, guid: a97c105638bdf8b4a8650670310a4cd3, type: 2}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 1238678705
  m_SortingLayer: -2
  m_SortingOrder: 0
  m_ChunkSize: {x: 32, y: 32, z: 32}
  m_ChunkCullingBounds: {x: 0, y: 0, z: 0}
  m_MaxChunkCount: 16
  m_MaxFrameAge: 16
  m_SortOrder: 0
  m_Mode: 0
  m_DetectChunkCullingBounds: 0
  m_MaskInteraction: 0
""" % go_id


def collider_header(class_tag, file_id, go_id):
    return """--- !u!%s &%d
%s:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  serializedVersion: 3
  m_Density: 1
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_ForceSendLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ForceReceiveLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ContactCaptureLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_CallbackLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_IsTrigger: 0
  m_UsedByEffector: 0
""" % (class_tag, file_id, class_tag.split(" ")[-1] if False else {
    "19719996": "TilemapCollider2D",
    "66": "CompositeCollider2D",
}[class_tag], go_id)


def level_prefab(level_id, display_name, cells, spawns, rects, tile_guids, sprite_guids, terrain_guid, marker_guid):
    children = ["  - {fileID: 200002}"]
    marker_blocks = []
    next_id = 300001
    for spawn in spawns:
        rect = rects[spawn["surface"]]
        leash_min, leash_max = fit_patrol(rect, spawn["leashMin"], spawn["leashMax"], spawn["inset"])
        x = min(max(spawn["x"], rect["xMin"]), rect["xMax"])
        y = rect["yMax"] + spawn["yOffset"]
        go_id = next_id
        tr_id = next_id + 1
        comp_id = next_id + 2
        next_id += 3
        children.append("  - {fileID: %d}" % tr_id)
        marker_blocks.append("""--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: Spawn_%s
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: %s, y: %s, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 100002}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  spawnId: %s
  leashMin: %s
  leashMax: %s
""" % (
            go_id, tr_id, comp_id, spawn["id"], tr_id, go_id, trim(x), trim(y),
            comp_id, go_id, marker_guid, spawn["id"], trim(leash_min), trim(leash_max),
        ))

    tilemap = tilemap_yaml(cells, tile_guids, sprite_guids)
    parts = ["""%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 100002}
  - component: {fileID: 100003}
  - component: {fileID: 100004}
  m_Layer: 0
  m_Name: %s
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &100002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
%s
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!156049354 &100003
Grid:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100001}
  m_Enabled: 1
  m_CellSize: {x: 1, y: 1, z: 0}
  m_CellGap: {x: 0, y: 0, z: 0}
  m_CellLayout: 0
  m_CellSwizzle: 0
--- !u!114 &100004
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  levelId: %s
--- !u!1 &200001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 200002}
  - component: {fileID: 200003}
  - component: {fileID: 200004}
  - component: {fileID: 200005}
  - component: {fileID: 200006}
  - component: {fileID: 200007}
  m_Layer: 6
  m_Name: Terrain
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &200002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 100002}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1839735485 &200003
Tilemap:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200001}
  m_Enabled: 1
%s
""" % (display_name, "\n".join(children), terrain_guid, level_id, tilemap)]
    parts.append(renderer_yaml(200001))
    parts.append("""--- !u!50 &200005
Rigidbody2D:
  serializedVersion: 5
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200001}
  m_BodyType: 2
  m_Simulated: 1
  m_UseFullKinematicContacts: 0
  m_UseAutoMass: 0
  m_Mass: 1
  m_LinearDamping: 0
  m_AngularDamping: 0.05
  m_GravityScale: 0
  m_Material: {fileID: 0}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_Interpolate: 0
  m_SleepingMode: 1
  m_CollisionDetection: 0
  m_Constraints: 0
""")
    parts.append(collider_header("19719996", 200006, 200001))
    parts.append("""  m_CompositeOperation: 1
  m_CompositeOrder: 0
  m_Offset: {x: 0, y: 0}
  m_MaximumTileChangeCount: 1000
  m_ExtrusionFactor: 0
  m_UseDelaunayMesh: 0
""")
    parts.append(collider_header("66", 200007, 200001))
    parts.append("""  m_CompositeOperation: 0
  m_CompositeOrder: 0
  m_Offset: {x: 0, y: 0}
  m_GeometryType: 1
  m_GenerationType: 0
  m_EdgeRadius: 0
  m_ColliderPaths:
  - m_Collider: {fileID: 200006}
  m_CompositePaths:
    m_Paths: []
  m_VertexDistance: 0.0005
  m_OffsetDistance: 0.00005
  m_UseDelaunayMesh: 1
  m_CompositeGameObject: {fileID: 200001}
""")
    parts.extend(marker_blocks)
    return "".join(parts)


def palette_prefab(tile_guids, sprite_guids):
    cells = {}
    rows = [("stone", 2), ("dirt", 1), ("wall", 0)]
    for prefix, y in rows:
        for i in range(16):
            cells[(i, y)] = {"tile": "%s_%d" % (prefix, i), "block": prefix}
    tilemap = tilemap_yaml(cells, tile_guids, sprite_guids)
    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!1 &100001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 100002}
  - component: {fileID: 100003}
  m_Layer: 0
  m_Name: Ground
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &100002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 200002}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!156049354 &100003
Grid:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 100001}
  m_Enabled: 1
  m_CellSize: {x: 1, y: 1, z: 0}
  m_CellGap: {x: 0, y: 0, z: 0}
  m_CellLayout: 0
  m_CellSwizzle: 0
--- !u!1 &200001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 200002}
  - component: {fileID: 200003}
  - component: {fileID: 200004}
  m_Layer: 0
  m_Name: Layer1
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &200002
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 100002}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1839735485 &200003
Tilemap:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 200001}
  m_Enabled: 1
%s
%s--- !u!114 &8185616529796923533
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 12395, guid: 0000000000000000e000000000000000, type: 0}
  m_Name: Palette Settings
  m_EditorClassIdentifier: 
  cellSizing: 0
  m_TransparencySortMode: 0
  m_TransparencySortAxis: {x: 0, y: 0, z: 1}
""" % (tilemap, renderer_yaml(200001).replace("&200004", "&200004"))


def set_asset(prefab_guids, set_guid_script):
    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: LevelTerrainSet
  m_EditorClassIdentifier: 
  basicCombat: {fileID: 100004, guid: %s, type: 3}
  foolTrial: {fileID: 100004, guid: %s, type: 3}
  echoRoom: {fileID: 100004, guid: %s, type: 3}
  bossArena: {fileID: 100004, guid: %s, type: 3}
""" % (
        set_guid_script,
        prefab_guids["basic"],
        prefab_guids["fool"],
        prefab_guids["chariot"],
        prefab_guids["boss"],
    )


def fmt_delta(new, old):
    delta = new - old
    sign = "+" if delta > 0.0005 else ""
    return "%s%s" % (sign, trim(delta))


def main():
    for path in SCRIPT_GUID:
        SCRIPT_GUID[path] = guid_for(path)
        script_meta(os.path.join(ROOT, path + ".meta"), SCRIPT_GUID[path])

    for folder, name in (
        ("Assets/Tiles", "tiles-root"),
        ("Assets/Tiles/Tiles", "tiles-assets"),
        ("Assets/Tiles/Levels", "tiles-levels"),
        ("Assets/Tiles/Palettes", "tiles-palettes"),
        ("Assets/Resources/LevelTerrain", "tiles-set"),
    ):
        folder_meta(os.path.join(ROOT, folder + ".meta"), guid_for(name))

    blocks, spawns = parse_catalog()
    names = []
    for prefix, count in (("stone", 16), ("dirt", 16), ("wall", 16)):
        for i in range(count):
            names.append("%s_%d" % (prefix, i))
    sprite_guids = {name: sprite_guid(name) for name in names}
    tile_guids = {name: guid_for("tile:" + name) for name in names}
    for name in names:
        lift = DIRT_LIFT if name.startswith("dirt_") else STONE_LIFT
        write(os.path.join(ROOT, TILE_DIR, name + ".asset"), tile_asset(name, sprite_guids[name], lift))
        asset_meta(os.path.join(ROOT, TILE_DIR, name + ".asset.meta"), tile_guids[name])

    report = []
    prefab_guids = {}
    for level, display in LEVEL_PREFAB.items():
        level_blocks = [block for block in blocks if block["level"] == level]
        cells, overwrites = paint(level_blocks)
        rects = {}
        report.append("## %s" % display)
        for block in level_blocks:
            rect = block["rect"]
            rects[block["name"]] = rect
            report.append(
                "- %s 原范围 x %.3f..%.3f y %.3f..%.3f → 格子 x %d..%d y %d..%d（边 %s / %s / %s / %s）"
                % (
                    block["name"],
                    rect["left0"], rect["right0"], rect["bottom0"], rect["top0"],
                    rect["xMin"], rect["xMax"], rect["yMin"], rect["yMax"],
                    fmt_delta(rect["xMin"], rect["left0"]),
                    fmt_delta(rect["xMax"], rect["right0"]),
                    fmt_delta(rect["yMin"], rect["bottom0"]),
                    fmt_delta(rect["yMax"], rect["top0"]),
                )
            )
        for item in overwrites:
            report.append("- 覆盖 %s 盖住 %s @ (%d,%d)" % (item[1], item[2], item[3], item[4]))
        level_spawns = [spawn for spawn in spawns if spawn["level"] == level]
        prefab_guids[level] = guid_for("prefab:" + display)
        text = level_prefab(
            level, display, cells, level_spawns, rects, tile_guids, sprite_guids,
            SCRIPT_GUID["Assets/Scripts/Level/LevelTerrain.cs"],
            SCRIPT_GUID["Assets/Scripts/Level/LevelSpawnMarker.cs"],
        )
        path = os.path.join(ROOT, LEVEL_DIR, display + ".prefab")
        write(path, text)
        prefab_meta(path + ".meta", prefab_guids[level])
        report.append("- 格子数 %d" % len(cells))

    palette_guid = guid_for("palette:Ground")
    write(os.path.join(ROOT, PALETTE_PATH), palette_prefab(tile_guids, sprite_guids))
    prefab_meta(os.path.join(ROOT, PALETTE_PATH + ".meta"), palette_guid)
    write(
        os.path.join(ROOT, SET_PATH),
        set_asset(prefab_guids, SCRIPT_GUID["Assets/Scripts/Level/LevelTerrainSet.cs"]),
    )
    asset_meta(os.path.join(ROOT, SET_PATH + ".meta"), guid_for("set:LevelTerrainSet"))

    print("\n".join(report))


if __name__ == "__main__":
    main()
