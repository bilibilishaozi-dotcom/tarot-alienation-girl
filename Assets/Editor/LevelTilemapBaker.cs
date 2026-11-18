using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 菜单把 LevelTerrainCatalog 里的方块铺进四关 Tilemap。格子规则和运行时对位是同一份。
public static class LevelTilemapBaker
{
    private const string TileFolder = "Assets/Tiles/Tiles";
    private const string LevelFolder = "Assets/Tiles/Levels";
    private const string PalettePath = "Assets/Tiles/Palettes/Ground.prefab";
    private const string SetPath = "Assets/Resources/LevelTerrain/LevelTerrainSet.asset";
    private const string SpriteFolder = "Assets/Resources/Art/Tiles";

    [MenuItem("关卡/重新烘焙四关 Tilemap")]
    public static void BakeAll()
    {
        Directory.CreateDirectory(TileFolder);
        Directory.CreateDirectory(LevelFolder);
        Directory.CreateDirectory("Assets/Tiles/Palettes");
        Directory.CreateDirectory("Assets/Resources/LevelTerrain");

        Dictionary<string, Tile> tiles = EnsureTiles();
        string[] ids = LevelTerrainCatalog.LevelIds;
        var terrains = new LevelTerrain[ids.Length];
        for (int i = 0; i < ids.Length; i++)
            terrains[i] = BakeLevel(ids[i], tiles);

        EnsurePalette(tiles);
        WriteSet(terrains, ids);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("四关 Tilemap 已按当前代码地形重新烘焙。");
    }

    [MenuItem("关卡/在场景里显示/基础战斗")]
    public static void ShowBasic() => ShowInScene("basic");

    [MenuItem("关卡/在场景里显示/愚者试用")]
    public static void ShowFool() => ShowInScene("fool");

    [MenuItem("关卡/在场景里显示/牌之回响")]
    public static void ShowEcho() => ShowInScene("chariot");

    [MenuItem("关卡/在场景里显示/终局")]
    public static void ShowBoss() => ShowInScene("boss");

    private static void ShowInScene(string levelId)
    {
        string path = LevelFolder + "/" + LevelTerrainCatalog.PrefabName(levelId) + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("还没有预制体 " + path + "。先跑「关卡/重新烘焙四关 Tilemap」。");
            return;
        }

        LevelTerrain[] existing = Object.FindObjectsByType<LevelTerrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        LevelTerrain match = null;
        for (int i = 0; i < existing.Length; i++)
        {
            bool on = existing[i].levelId == levelId;
            existing[i].gameObject.SetActive(on);
            if (on)
                match = existing[i];
        }

        if (match == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "LevelTerrain_" + levelId;
            Undo.RegisterCreatedObjectUndo(instance, "显示关卡地形");
            match = instance.GetComponent<LevelTerrain>();
        }

        if (match != null)
        {
            Selection.activeGameObject = match.gameObject;
            EditorGUIUtility.PingObject(match.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static Dictionary<string, Tile> EnsureTiles()
    {
        var tiles = new Dictionary<string, Tile>();
        AddSet(tiles, "stone", 16, 1f - 29f / 32f);
        AddSet(tiles, "dirt", 16, 1f - 23f / 32f);
        AddSet(tiles, "wall", 16, 1f - 29f / 32f);
        return tiles;
    }

    private static void AddSet(Dictionary<string, Tile> tiles, string prefix, int count, float lift)
    {
        for (int i = 0; i < count; i++)
        {
            string name = prefix + "_" + i;
            string path = TileFolder + "/" + name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/" + name + ".png");
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.flags = TileFlags.LockColor;
            tile.colliderType = Tile.ColliderType.Grid;
            tile.transform = Matrix4x4.TRS(new Vector3(0f, lift, 0f), Quaternion.identity, Vector3.one);
            EditorUtility.SetDirty(tile);
            tiles[name] = tile;
        }
    }

    private static LevelTerrain BakeLevel(string levelId, Dictionary<string, Tile> tiles)
    {
        string path = LevelFolder + "/" + LevelTerrainCatalog.PrefabName(levelId) + ".prefab";
        var root = new GameObject(LevelTerrainCatalog.PrefabName(levelId));
        Grid grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);
        LevelTerrain terrain = root.AddComponent<LevelTerrain>();
        terrain.levelId = levelId;

        var mapObject = new GameObject("Terrain");
        mapObject.layer = LayerMask.NameToLayer("Ground");
        mapObject.transform.SetParent(root.transform, false);
        Tilemap map = mapObject.AddComponent<Tilemap>();
        map.tileAnchor = new Vector3(0f, 0f, 0f);
        TilemapRenderer renderer = mapObject.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Ground";
        renderer.sortingOrder = 0;

        Rigidbody2D body = mapObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        body.gravityScale = 0f;
        TilemapCollider2D tileCollider = mapObject.AddComponent<TilemapCollider2D>();
        CompositeCollider2D composite = mapObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        var previous = new Dictionary<int, int>();
        foreach (LevelTerrainCatalog.Block block in LevelTerrainCatalog.Blocks(levelId))
        {
            LevelTerrainCatalog.CellRect rect = LevelTerrainCatalog.Rasterize(block);
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                previous[y] = -1;
                for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    int prior = previous[y];
                    string tileName = LevelTerrainCatalog.TileName(block, rect, x, y, ref prior);
                    previous[y] = prior;
                    if (tiles.TryGetValue(tileName, out Tile tile))
                        map.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        foreach (LevelTerrainCatalog.Spawn spawn in LevelTerrainCatalog.Spawns(levelId))
        {
            LevelTerrainCatalog.Pose pose = LevelTerrainCatalog.PoseOf(levelId, spawn.id);
            var markerObject = new GameObject("Spawn_" + spawn.id);
            markerObject.transform.SetParent(root.transform, false);
            markerObject.transform.position = new Vector3(pose.x, pose.y, 0f);
            LevelSpawnMarker marker = markerObject.AddComponent<LevelSpawnMarker>();
            marker.spawnId = spawn.id;
            marker.leashMin = pose.leashMin;
            marker.leashMax = pose.leashMax;
        }

        try
        {
            composite.GenerateGeometry();
        }
        catch (System.Exception)
        {
            Debug.LogWarning("复合碰撞网格要等预制体进场景后才会生成。进 Play 或选中 Terrain 即可。");
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<LevelTerrain>(path);
    }

    // 调色板预制体上有 Unity 内部的 Palette Settings。整份重存容易把它丢掉，所以文件已经在时不再改。
    private static void EnsurePalette(Dictionary<string, Tile> tiles)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) != null)
            return;

        var root = new GameObject("Ground");
        Grid grid = root.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);
        var layer = new GameObject("Layer1");
        layer.transform.SetParent(root.transform, false);
        Tilemap map = layer.AddComponent<Tilemap>();
        map.tileAnchor = new Vector3(0f, 0f, 0f);
        layer.AddComponent<TilemapRenderer>();
        PaintRow(map, tiles, "stone", 16, 2);
        PaintRow(map, tiles, "dirt", 16, 1);
        PaintRow(map, tiles, "wall", 16, 0);
        PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
        Object.DestroyImmediate(root);
        Debug.LogWarning("新建了地面调色板，但没有 Palette Settings。请在 Tile Palette 窗口里用 Create New Palette，或保留仓库里的 Ground.prefab。");
    }

    private static void PaintRow(Tilemap map, Dictionary<string, Tile> tiles, string prefix, int count, int y)
    {
        for (int i = 0; i < count; i++)
        {
            if (tiles.TryGetValue(prefix + "_" + i, out Tile tile))
                map.SetTile(new Vector3Int(i, y, 0), tile);
        }
    }

    private static void WriteSet(LevelTerrain[] terrains, string[] ids)
    {
        LevelTerrainSet set = AssetDatabase.LoadAssetAtPath<LevelTerrainSet>(SetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<LevelTerrainSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == "fool")
                set.foolTrial = terrains[i];
            else if (ids[i] == "chariot")
                set.echoRoom = terrains[i];
            else if (ids[i] == "boss")
                set.bossArena = terrains[i];
            else
                set.basicCombat = terrains[i];
        }

        EditorUtility.SetDirty(set);
    }
}
