using System.Collections.Generic;
using UnityEngine;

// 四关静态地形的原始方块。编辑器烘焙和运行时对位都读这里。
// 格子是 1x1。一块方块盖住某格至少一半就占这格；不到一半时，只留重叠最多的那一格。
// 高度打平时，选顶边更接近原顶的那一格。左右打平时，选更靠右的那一格（右墙中心正好落在格线上）。
public static class LevelTerrainCatalog
{
    public struct Block
    {
        public string levelId;
        public string name;
        public Vector2 center;
        public Vector2 size;
        public bool endCaps;

        public Block(string levelId, string name, float cx, float cy, float sx, float sy, bool endCaps)
        {
            this.levelId = levelId;
            this.name = name;
            this.center = new Vector2(cx, cy);
            this.size = new Vector2(sx, sy);
            this.endCaps = endCaps;
        }

        public float Left => center.x - size.x * 0.5f;
        public float Right => center.x + size.x * 0.5f;
        public float Bottom => center.y - size.y * 0.5f;
        public float Top => center.y + size.y * 0.5f;
    }

    public struct CellRect
    {
        public int xMin;
        public int xMax;
        public int yMin;
        public int yMax;

        public float Left => xMin;
        public float Right => xMax;
        public float Bottom => yMin;
        public float Top => yMax;
        public int Width => xMax - xMin;
        public int Height => yMax - yMin;
    }

    // 站在某块地形上的生成点。yOffset 加在烘焙后的台面顶上。
    // 孢子、骷髅的轴心在台面上方 0.5；甲虫的轴心就在台面上。
    public struct Spawn
    {
        public string levelId;
        public string id;
        public string surface;
        public float x;
        public float yOffset;
        public float leashMin;
        public float leashMax;
        public float inset;

        public Spawn(string levelId, string id, string surface, float x, float yOffset, float leashMin, float leashMax, float inset)
        {
            this.levelId = levelId;
            this.id = id;
            this.surface = surface;
            this.x = x;
            this.yOffset = yOffset;
            this.leashMin = leashMin;
            this.leashMax = leashMax;
            this.inset = inset;
        }
    }

    public struct Pose
    {
        public float x;
        public float y;
        public float leashMin;
        public float leashMax;
    }

    // 这些数字就是 FirstLevel 里原来的 Solid 中心和尺寸。endCaps 为真时，顶行左右两端用 stone_4 / stone_15。
    private static readonly Block[] blocks =
    {
        new Block("basic", "Floor", 18.8f, -1f, 40f, 2f, false),
        new Block("basic", "LeftWall", -1.7f, 3.2f, 0.7f, 6.4f, false),
        new Block("basic", "RightWall", 39f, 3.4f, 0.7f, 6.8f, false),
        new Block("basic", "PlatformA", 6.3f, 1.0f, 4.8f, 0.4f, true),
        new Block("basic", "PlatformB", 14.6f, 1.8f, 4.8f, 0.4f, true),
        new Block("basic", "LowLip", 21.6f, 0.31f, 3.6f, 0.62f, false),
        new Block("basic", "EnemyPlinth", 25.6f, 0.14f, 1.8f, 0.28f, false),
        new Block("basic", "HighLedge", 35.4f, 1.7f, 6.4f, 3.4f, false),

        new Block("fool", "Floor", 16.25f, -1f, 36.5f, 2f, false),
        new Block("fool", "LeftWall", -2.35f, 3.75f, 0.7f, 7.5f, false),
        new Block("fool", "RightWall", 34.15f, 3f, 0.7f, 6f, false),
        new Block("fool", "Arch", 20.4f, 5.5f, 0.75f, 3.8f, false),

        new Block("chariot", "Floor", 35.75f, -1f, 74.5f, 2f, false),
        new Block("chariot", "LeftWall", -1.8f, 3f, 0.7f, 6f, false),
        new Block("chariot", "RightWall", 73.3f, 3f, 0.7f, 6f, false),
        new Block("chariot", "FinaleLintel", 44.6f, 2.45f, 4.2f, 0.35f, false),

        new Block("boss", "Floor", 14f, -1f, 30.4f, 2f, false),
        new Block("boss", "LeftWall", -1.5f, 3.6f, 0.8f, 7.2f, false),
        new Block("boss", "RightWall", 29.5f, 3.6f, 0.8f, 7.2f, false),
        new Block("boss", "LedgeA", 7.5f, 1.5f, 4f, 0.4f, false),
        new Block("boss", "LedgeB", 13f, 2.6f, 3.6f, 0.4f, false),
        new Block("boss", "LedgeC", 19.5f, 3.4f, 3.4f, 0.4f, false),
        new Block("boss", "LedgeD", 26.5f, 3.4f, 3f, 0.4f, false),
    };

    private static readonly Spawn[] spawns =
    {
        new Spawn("basic", "Spore", "PlatformA", 6.3f, 0.5f, 4.4f, 8.2f, 0.15f),
        new Spawn("basic", "Skeleton", "PlatformB", 14.6f, 0.5f, 12.4f, 16.8f, 0.15f),
        new Spawn("basic", "Beetle", "EnemyPlinth", 25.6f, 0f, 24.95f, 26.45f, 0.15f),
    };

    public static IEnumerable<Block> Blocks(string levelId)
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].levelId == levelId)
                yield return blocks[i];
        }
    }

    public static string[] LevelIds => new[] { "basic", "fool", "chariot", "boss" };

    public static string PrefabName(string levelId)
    {
        if (levelId == "fool")
            return "FoolTrial";
        if (levelId == "chariot")
            return "EchoRoom";
        if (levelId == "boss")
            return "BossArena";
        return "BasicCombat";
    }

    public static bool TryGet(string levelId, string name, out Block block)
    {
        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].levelId == levelId && blocks[i].name == name)
            {
                block = blocks[i];
                return true;
            }
        }

        block = default;
        return false;
    }

    public static CellRect Rasterize(Block block)
    {
        List<int> xs = AxisCells(block.Left, block.Right, false);
        List<int> ys = AxisCells(block.Bottom, block.Top, true);
        CellRect rect = new CellRect
        {
            xMin = xs[0],
            xMax = xs[xs.Count - 1] + 1,
            yMin = ys[0],
            yMax = ys[ys.Count - 1] + 1
        };
        // 门楣是头顶的薄梁。按最近格它的底会落到 2.0，玩家站在地上时头顶大约 2.02，通道会被堵住。
        // 这一块单独上移一格，底放到 3，人仍能从下面走过去。
        if (block.name == "FinaleLintel")
        {
            rect.yMin += 1;
            rect.yMax += 1;
        }

        return rect;
    }

    public static Pose PoseOf(string levelId, string spawnId)
    {
        for (int i = 0; i < spawns.Length; i++)
        {
            if (spawns[i].levelId != levelId || spawns[i].id != spawnId)
                continue;

            Spawn spawn = spawns[i];
            CellRect surface = Rasterize(Must(levelId, spawn.surface));
            FitPatrol(surface, spawn.leashMin, spawn.leashMax, spawn.inset, out float min, out float max);
            float x = Mathf.Clamp(spawn.x, surface.Left, surface.Right);
            return new Pose
            {
                x = x,
                y = surface.Top + spawn.yOffset,
                leashMin = min,
                leashMax = max
            };
        }

        return default;
    }

    public static IEnumerable<Spawn> Spawns(string levelId)
    {
        for (int i = 0; i < spawns.Length; i++)
        {
            if (spawns[i].levelId == levelId)
                yield return spawns[i];
        }
    }

    public static void FitPatrol(CellRect surface, float originalMin, float originalMax, float inset, out float min, out float max)
    {
        float left = surface.Left + inset;
        float right = surface.Right - inset;
        if (right < left)
        {
            float mid = (surface.Left + surface.Right) * 0.5f;
            left = mid;
            right = mid;
        }

        min = Mathf.Max(originalMin, left);
        max = Mathf.Min(originalMax, right);
        if (max < min)
        {
            min = left;
            max = right;
        }
    }

    public static Block Must(string levelId, string name)
    {
        if (TryGet(levelId, name, out Block block))
            return block;
        throw new System.InvalidOperationException("地形里没有 " + levelId + "/" + name);
    }

    // 窄而高的一整列用墙砖。其余顶行是石面，下面是土。薄台只有一行，所以全是石面。
    public static string TileName(Block block, CellRect rect, int x, int y, ref int previous)
    {
        bool wall = rect.Width == 1 && rect.Height >= 2;
        int rowFromTop = rect.yMax - 1 - y;
        if (wall)
        {
            int variant = Variant(block, x, rowFromTop, 3, previous, 16);
            previous = variant;
            return "wall_" + variant;
        }

        bool top = y == rect.yMax - 1;
        if (!top)
        {
            int dirt = Variant(block, x, rowFromTop, 2, previous, 16);
            previous = dirt;
            return "dirt_" + dirt;
        }

        if (block.endCaps && x == rect.xMin)
        {
            previous = 4;
            return "stone_4";
        }

        if (block.endCaps && rect.Width >= 2 && x == rect.xMax - 1)
        {
            previous = 15;
            return "stone_15";
        }

        int stone = Variant(block, x, rowFromTop, 1, previous, 16);
        previous = stone;
        return "stone_" + stone;
    }

    // 和 FirstLevel.TileVariant 同一套整数哈希，种子改成格子坐标，避免每次运行变。
    private static int Variant(Block block, int col, int row, int kind, int avoid, int count)
    {
        if (count <= 1)
            return 0;

        unchecked
        {
            int hash = 216613626;
            string blockName = block.name;
            for (int i = 0; i < blockName.Length; i++)
                hash = (hash * 16777619) ^ blockName[i];
            hash = (hash * 16777619) ^ (Mathf.RoundToInt(block.center.x * 100f) * 17);
            hash = (hash * 16777619) ^ (Mathf.RoundToInt(block.center.y * 100f) * 31);
            hash = (hash * 16777619) ^ (col * 13);
            hash = (hash * 16777619) ^ (row * 29);
            hash = (hash * 16777619) ^ kind;
            int index = (hash & 0x7fffffff) % count;
            if (index == avoid)
                index = (index + 1) % count;
            return index;
        }
    }

    private static List<int> AxisCells(float a, float b, bool vertical)
    {
        int first = EdgeIndex(a, true);
        int last = EdgeIndex(b, false);
        var strong = new List<int>();
        var weak = new List<int>();
        float best = 0f;
        for (int i = first; i <= last; i++)
        {
            float overlap = Mathf.Min(b, i + 1f) - Mathf.Max(a, i);
            if (overlap <= 0.0001f)
                continue;
            if (overlap >= 0.5f - 0.0001f)
                strong.Add(i);
            if (weak.Count == 0 || overlap > best + 0.0001f)
            {
                best = overlap;
                weak.Clear();
                weak.Add(i);
            }
            else if (Mathf.Abs(overlap - best) <= 0.0001f)
            {
                weak.Add(i);
            }
        }

        if (strong.Count > 0)
            return strong;
        if (weak.Count == 0)
            return new List<int> { first };
        if (weak.Count == 1)
            return weak;

        int chosen = weak[0];
        if (vertical)
        {
            float bestDist = Mathf.Abs(chosen + 1f - b);
            for (int i = 1; i < weak.Count; i++)
            {
                float dist = Mathf.Abs(weak[i] + 1f - b);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    chosen = weak[i];
                }
            }
        }
        else
        {
            chosen = weak[weak.Count - 1];
        }

        return new List<int> { chosen };
    }

    private static int EdgeIndex(float value, bool start)
    {
        float nearest = Mathf.Round(value);
        if (Mathf.Abs(value - nearest) < 0.0001f)
            return start ? Mathf.RoundToInt(nearest) : Mathf.RoundToInt(nearest) - 1;
        return Mathf.FloorToInt(value);
    }
}
