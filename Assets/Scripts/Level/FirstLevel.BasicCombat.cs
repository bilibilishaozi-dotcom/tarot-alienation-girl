using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 第一关基础战斗：三名敌人、蹬墙跳，以及通向愚者试用的出口。
public partial class FirstLevel
{
    private void BuildBasicCombat(int ground)
    {
        const float floorTop = 0f;
        Solid("Floor", new Vector2(14.9f, floorTop - 1f), new Vector2(32.2f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-1.7f, 3.2f), new Vector2(0.7f, 6.4f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(31.7f, 3.4f), new Vector2(0.7f, 6.8f), new Color(0.22f, 0.19f, 0.17f), ground, 0);

        // 两座薄平台。哨兵只在台面上巡逻，追击和受击也不会走下边缘。
        const float deckTop = 1.7f;
        const float deckThickness = 0.4f;
        float deckCenterY = deckTop - deckThickness * 0.5f;
        Solid("PlatformA", new Vector2(5.8f, deckCenterY), new Vector2(4.8f, deckThickness), new Color(0.46f, 0.38f, 0.29f), ground, 1);
        Solid("PlatformB", new Vector2(12f, deckCenterY), new Vector2(4.8f, deckThickness), new Color(0.46f, 0.38f, 0.29f), ground, 1);
        Sign("AttackSign", root, new Vector3(5.8f, 2.65f, 0f), "鼠标左键", 180f);
        Sign("DashSign", root, new Vector3(12f, 2.65f, 0f), "左 Shift 躲开", 220f);

        // 低台大约 0.62 高。跳跃攻击的刀光贴近身体，离开低台、面向右侧敌人时按鼠标左键能打到。
        const float lipTop = 0.62f;
        Solid("LowLip", new Vector2(17.4f, lipTop * 0.5f), new Vector2(3.6f, lipTop), new Color(0.43f, 0.36f, 0.28f), ground, 1);
        const float plinthTop = 0.28f;
        Solid("EnemyPlinth", new Vector2(20.6f, plinthTop * 0.5f), new Vector2(1.8f, plinthTop), new Color(0.34f, 0.28f, 0.24f), ground, 1);
        Sign("AirSign", root, new Vector3(18.2f, 1.55f, 0f), "空中攻击", 180f);

        float feetLift = 0.92f;
        arena = new BasicEnemy[3];
        arena[0] = SpawnLeashed("SentryA", 5.8f, deckTop + feetLift, "哨兵甲", new Color(0.72f, 0.28f, 0.22f), 4f, 7.6f, ground);
        arena[1] = SpawnLeashed("SentryB", 12f, deckTop + feetLift, "哨兵乙", new Color(0.28f, 0.38f, 0.72f), 10.2f, 13.8f, ground);
        arena[1].SetAsCharger();
        arena[2] = SpawnLeashed("LowEnemy", 20.6f, plinthTop + feetLift, "低处敌人", new Color(0.62f, 0.28f, 0.78f), 20.25f, 20.95f, ground);

        // 普通跳约 2.46，蹬墙跳在贴墙后再抬约 2.10。柱底 2.40 让人能走过去，台面 3.40 让普通跳和冲刺都上不去。
        basicSeal = Solid("ExitSeal", new Vector2(22.35f, 1.55f), new Vector2(0.55f, 3.1f), new Color(0.45f, 0.2f, 0.22f), ground, 3);
        Solid("ClimbPillar", new Vector2(23.7f, 4.25f), new Vector2(0.7f, 3.7f), new Color(0.62f, 0.66f, 0.72f), ground, 2);
        Solid("HighLedge", new Vector2(27.85f, 1.7f), new Vector2(4.9f, 3.4f), new Color(0.38f, 0.32f, 0.28f), ground, 1);
        Sign("WallJumpSign", root, new Vector3(24.7f, 1.45f, 0f), "蹬墙跳", 160f);
        Sign("ExitLabel", root, new Vector3(29.2f, 4.55f, 0f), "出口", 140f);
        CreateBasicExit(29.2f, 4.7f);
    }

    private BasicEnemy SpawnLeashed(string objectName, float x, float y, string displayName, Color color, float minX, float maxX, int ground)
    {
        BasicEnemy enemy = SpawnEnemy(objectName, x, 3, false, displayName, color, "Player", 6);
        enemy.transform.position = new Vector3(x, y, 0f);
        enemy.SetAsPractice();
        enemy.StayOnPlatform(minX, maxX, 1 << ground);
        enemy.Died += OnArenaEnemyDied;
        return enemy;
    }

    private void CreateBasicExit(float x, float y)
    {
        GameObject exit = new GameObject("LevelExit");
        exit.transform.SetParent(root, false);
        exit.transform.position = new Vector3(x, y, 0f);
        BoxCollider2D trigger = exit.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.6f, 2.6f);
        exit.AddComponent<LevelExit>().Arm(this);
    }

    private void OnArenaEnemyDied()
    {
        if (basicExitOpen || arena == null)
            return;

        for (int i = 0; i < arena.Length; i++)
        {
            if (arena[i] != null && !arena[i].IsDead)
                return;
        }

        OpenBasicExit();
    }

    private void OpenBasicExit()
    {
        if (basicExitOpen)
            return;

        basicExitOpen = true;
        ReleaseSeal();
        RefreshObjective();
    }

    private void ReleaseSeal()
    {
        if (basicSeal == null)
            return;

        Collider2D[] blocking = basicSeal.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < blocking.Length; i++)
            blocking[i].enabled = false;
        AttackFeedback.Thud(basicSeal.transform.position);
        StartCoroutine(SinkSeal());
    }

    private IEnumerator SinkSeal()
    {
        yield return SinkDown(basicSeal, 3.6f, 0.45f);
    }

    private void RefreshBasicObjective()
    {
        float x = player != null ? player.transform.position.x : 0f;
        if (basicExitOpen && x > 26.5f)
        {
            SetObjective("走进出口，进入愚者牌试用");
            return;
        }

        if (basicExitOpen)
        {
            SetObjective("贴灰柱按住 A，空格蹬出后松开 A");
            return;
        }

        if (x < 3f)
        {
            SetObjective("WASD 移动，空格跳跃，左 Shift 冲刺");
            return;
        }

        if (ArenaAlive(2) && x > 14.6f)
        {
            SetObjective("跳起后马上按鼠标左键，或离开低台时按");
            return;
        }

        if (ArenaAlive(1) && x > 8.4f)
        {
            SetObjective("哨兵挥刀前，左 Shift 冲刺躲开");
            return;
        }

        if (ArenaAlive(0) || ArenaAlive(1))
        {
            SetObjective("跳上平台，鼠标左键攻击哨兵");
            return;
        }

        SetObjective("三个敌人都倒下后，再去蹬墙跳");
    }

    private bool ArenaAlive(int index)
    {
        return arena != null && index >= 0 && index < arena.Length && arena[index] != null && !arena[index].IsDead;
    }
}
