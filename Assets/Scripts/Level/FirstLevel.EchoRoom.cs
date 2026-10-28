using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 牌之回响：三座单牌试炼，以及按愚者、战车、力量顺序拆开核心的收束。
public partial class FirstLevel
{
    private void BuildEchoRoom(int ground)
    {
        Solid("Floor", new Vector2(29.25f, -1f), new Vector2(61.5f, 2f), new Color(0.31f, 0.27f, 0.23f), ground, 0);
        Solid("LeftWall", new Vector2(-1.8f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Solid("RightWall", new Vector2(60.4f, 3f), new Vector2(0.7f, 6f), new Color(0.22f, 0.19f, 0.17f), ground, 0);
        Sign("HubSign", root, new Vector3(2.4f, 2.3f, 0f), "左边单牌，右边收束", 280f);

        MonsterCardData fool = Resources.Load<MonsterCardData>("Cards/Fool");
        MonsterCardData chariot = Resources.Load<MonsterCardData>("Cards/Chariot");
        MonsterCardData strength = Resources.Load<MonsterCardData>("Cards/Strength");
        if (fool == null || chariot == null || strength == null)
            Debug.LogWarning("牌之回响缺少愚者、战车或力量的卡牌资源。");

        echoFoolMark = PlaceSeal(new Vector3(6.2f, 1.15f, 0f), fool, "愚者", new Color(0.95f, 0.78f, 0.28f));
        BuildFoolStation(ground);
        echoChariotMark = PlaceSeal(new Vector3(16.2f, 1.15f, 0f), chariot, "战车", new Color(0.45f, 0.78f, 0.95f));
        BuildChariotStation(ground);
        echoStrengthMark = PlaceSeal(new Vector3(25.2f, 1.15f, 0f), strength, "力量", new Color(0.86f, 0.28f, 0.18f));
        BuildStrengthStation(ground);
        BuildFinale(ground, fool, chariot, strength);
        RefreshEchoMarks();
    }

    private Text PlaceSeal(Vector3 position, MonsterCardData card, string label, Color color)
    {
        GameObject seal = new GameObject(label + "Seal");
        seal.transform.SetParent(root, false);
        seal.transform.position = position;
        CreateSprite(seal.transform, "Stone", Vector3.zero, new Vector3(0.7f, 1.35f, 1f), color, "Player", 4);
        BoxCollider2D trigger = seal.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.4f, 1.8f);
        seal.AddComponent<CardSeal>().Arm(card);
        Sign("SealName", seal.transform, position + Vector3.up * 1.15f, label, 140f);
        return Sign("SealMark", seal.transform, position + Vector3.up * 1.7f, "未完成", 160f);
    }

    private void BuildFoolStation(int ground)
    {
        const float stand = 8.4f;
        Solid("FoolRail", new Vector2(9.7f, 0.85f), new Vector2(0.35f, 1.7f), new Color(0.55f, 0.58f, 0.66f), ground, 3);
        Sign("FoolHint", root, new Vector3(stand, 2.15f, 0f), "打过栏杆", 180f);
        echoDummy = SpawnEnemy("FoolTarget", stand + 2.45f, 4, true, "靶子", new Color(0.85f, 0.62f, 0.2f), "Player", 6);
        echoDummy.transform.position = new Vector3(stand + 2.45f, 0.92f, 0f);
        echoDummy.Died += OnEchoFoolDied;
    }

    private void BuildChariotStation(int ground)
    {
        Sign("ChariotHint", root, new Vector3(19.4f, 2.35f, 0f), "冲过去", 160f);
        echoLane = new BasicEnemy[3];
        float[] xs = { 18.3f, 19.6f, 20.9f };
        Color[] colors =
        {
            new Color(0.25f, 0.55f, 0.72f),
            new Color(0.2f, 0.42f, 0.68f),
            new Color(0.16f, 0.32f, 0.58f)
        };
        for (int i = 0; i < echoLane.Length; i++)
        {
            echoLane[i] = SpawnEnemy("EchoLane" + i, xs[i], 2, false, "列兵", colors[i], "Player", 6);
            echoLane[i].transform.position = new Vector3(xs[i], 0.92f, 0f);
            echoLane[i].StayOnPlatform(xs[i] - 0.35f, xs[i] + 0.35f, 1 << ground);
            echoLane[i].SetPushable();
            echoLane[i].Died += OnEchoLaneDied;
        }
    }

    private void BuildStrengthStation(int ground)
    {
        Solid("StrengthLip", new Vector2(27.6f, 0.6f), new Vector2(2.4f, 1.2f), new Color(0.55f, 0.24f, 0.18f), ground, 1);
        Sign("StrengthHint", root, new Vector3(27.6f, 2.15f, 0f), "坠地", 120f);
        echoCluster = new BasicEnemy[3];
        float[] xs = { 29.7f, 30.5f, 31.3f };
        for (int i = 0; i < echoCluster.Length; i++)
        {
            echoCluster[i] = SpawnEnemy("EchoSlam" + i, xs[i], 2, true, "矮靶", new Color(0.72f, 0.22f, 0.16f), "Player", 6);
            echoCluster[i].transform.position = new Vector3(xs[i], 0.92f, 0f);
            echoCluster[i].Died += OnEchoClusterDied;
        }

        GameObject pot = new GameObject("BreakablePot");
        pot.transform.SetParent(root, false);
        pot.transform.position = new Vector3(30.4f, 0.45f, 0f);
        pot.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        SpriteRenderer sprite = pot.AddComponent<SpriteRenderer>();
        sprite.sprite = RuntimeSprite.Pixel;
        sprite.color = new Color(0.62f, 0.28f, 0.16f, 1f);
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 4;
        BoxCollider2D box = pot.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        pot.AddComponent<BreakableProp>().Arm(1);
    }

    private void BuildFinale(int ground, MonsterCardData fool, MonsterCardData chariot, MonsterCardData strength)
    {
        PlaceSeal(new Vector3(34.2f, 1.15f, 0f), fool, "换愚者", new Color(0.95f, 0.78f, 0.28f));
        const float stand = 36.2f;
        Solid("FinaleRail", new Vector2(37.35f, 0.9f), new Vector2(0.35f, 1.8f), new Color(0.55f, 0.58f, 0.66f), ground, 3);
        Sign("FinaleFoolHint", root, new Vector3(stand, 2.2f, 0f), "打第一印", 180f);
        finaleSeal = CreateFinaleTarget("FirstSeal", new Vector3(stand + 2.55f, 1.9f, 0f), new Vector3(0.7f, 1.4f, 1f), new Color(0.95f, 0.78f, 0.28f, 1f), 2, OnFinaleSealBroken);
        Sign("FirstSealLabel", root, new Vector3(stand + 2.55f, 2.9f, 0f), "第一印", 140f);

        finaleGate = Solid("FinaleGate", new Vector2(40.3f, 1.6f), new Vector2(0.55f, 3.2f), new Color(0.45f, 0.36f, 0.18f), ground, 4);
        PlaceSeal(new Vector3(42.2f, 1.15f, 0f), chariot, "换战车", new Color(0.45f, 0.78f, 0.95f));
        Sign("FinaleDashHint", root, new Vector3(44.4f, 2.35f, 0f), "冲到核心", 180f);
        Solid("FinaleLintel", new Vector2(44.6f, 2.45f), new Vector2(4.2f, 0.35f), new Color(0.22f, 0.2f, 0.24f), ground, 2);
        finaleLane = new BasicEnemy[3];
        float[] xs = { 43.4f, 44.6f, 45.8f };
        for (int i = 0; i < finaleLane.Length; i++)
        {
            finaleLane[i] = SpawnEnemy("FinaleLane" + i, xs[i], 2, false, "阻路", new Color(0.35f, 0.62f, 0.78f), "Player", 6);
            finaleLane[i].transform.position = new Vector3(xs[i], 0.92f, 0f);
            finaleLane[i].StayOnPlatform(xs[i] - 0.3f, xs[i] + 0.3f, 1 << ground);
            finaleLane[i].SetPushable();
            finaleLane[i].Died += OnFinaleLaneDied;
        }

        PlaceSeal(new Vector3(48.2f, 1.15f, 0f), strength, "换力量", new Color(0.86f, 0.28f, 0.18f));
        Solid("FinaleLip", new Vector2(50.1f, 0.6f), new Vector2(2.2f, 1.2f), new Color(0.62f, 0.22f, 0.16f), ground, 1);
        Sign("FinaleSlamHint", root, new Vector3(50.1f, 2.15f, 0f), "砸核心", 140f);
        finaleCore = CreateFinaleTarget("RiftCore", new Vector3(52.15f, 1.05f, 0f), new Vector3(1.15f, 1.7f, 1f), new Color(0.72f, 0.55f, 0.95f, 1f), 3, OnFinaleCoreBroken);
        CreateSprite(finaleCore.transform, "CoreHeart", new Vector3(0f, 0.05f, 0f), new Vector3(0.38f, 0.48f, 1f), new Color(1f, 0.9f, 0.7f, 1f), "Player", 8);
    }

    private FinaleTarget CreateFinaleTarget(string name, Vector3 position, Vector3 scale, Color color, int health, UnityEngine.Events.UnityAction onBroken)
    {
        GameObject target = new GameObject(name);
        target.transform.SetParent(root, false);
        target.transform.position = position;
        target.transform.localScale = scale;
        SpriteRenderer sprite = target.AddComponent<SpriteRenderer>();
        sprite.sprite = RuntimeSprite.Pixel;
        sprite.color = color;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 6;
        BoxCollider2D box = target.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
        FinaleTarget finale = target.AddComponent<FinaleTarget>();
        finale.Arm(health, onBroken);
        return finale;
    }

    private void OnFinaleSealBroken()
    {
        finaleFoolBroken = true;
        if (finaleGate != null)
        {
            Collider2D[] blocking = finaleGate.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < blocking.Length; i++)
                blocking[i].enabled = false;
            AttackFeedback.Thud(finaleGate.transform.position);
            StartCoroutine(SinkObject(finaleGate, 3.4f));
        }

        RefreshObjective();
    }

    private void OnFinaleLaneDied()
    {
        if (!GroupDead(finaleLane))
            return;

        finaleLaneClear = true;
        RefreshObjective();
    }

    private void OnFinaleCoreBroken()
    {
        if (finaleEnding || cleared)
            return;

        finaleEnding = true;
        StartCoroutine(PlayCoreEcho());
    }

    private IEnumerator SinkObject(GameObject block, float distance)
    {
        yield return SinkDown(block, distance, 0.4f);
    }

    private IEnumerator PlayCoreEcho()
    {
        SetObjective("三张牌同时留下了痕迹");
        Vector3 point = finaleCore != null ? finaleCore.transform.position : new Vector3(52.15f, 1.05f, 0f);
        yield return ShowEchoBeat(point, new Color(1f, 0.82f, 0.28f, 0.9f), "愚者拉开距离");
        yield return ShowEchoBeat(point, new Color(0.45f, 0.82f, 1f, 0.9f), "战车冲到核心");
        yield return ShowEchoBeat(point, new Color(1f, 0.35f, 0.18f, 0.9f), "力量震碎核心");
        if (finaleCore != null)
            finaleCore.gameObject.SetActive(false);
        Complete();
    }

    private IEnumerator ShowEchoBeat(Vector3 point, Color color, string caption)
    {
        GameObject beat = new GameObject("CoreEcho");
        beat.transform.SetParent(root, false);
        beat.transform.position = point;
        beat.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        SpriteRenderer sprite = beat.AddComponent<SpriteRenderer>();
        sprite.sprite = RuntimeSprite.Pixel;
        sprite.color = color;
        sprite.sortingLayerName = "Player";
        sprite.sortingOrder = 20;
        Text label = Sign("EchoCaption", beat.transform, point + Vector3.up * 1.4f, caption, 280f);
        label.color = color;
        float elapsed = 0f;
        while (elapsed < 0.55f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.55f);
            beat.transform.localScale = Vector3.Lerp(new Vector3(0.4f, 0.4f, 1f), new Vector3(2.4f, 2.4f, 1f), t);
            Color faded = color;
            faded.a = 0.9f * (1f - t);
            sprite.color = faded;
            yield return null;
        }

        Destroy(beat);
    }

    private void OnEchoFoolDied()
    {
        echoFoolDone = true;
        RefreshEchoMarks();
    }

    private void OnEchoLaneDied()
    {
        if (!GroupDead(echoLane))
            return;

        echoChariotDone = true;
        RefreshEchoMarks();
    }

    private void OnEchoClusterDied()
    {
        if (!GroupDead(echoCluster))
            return;

        echoStrengthDone = true;
        RefreshEchoMarks();
    }

    private static bool GroupDead(BasicEnemy[] group)
    {
        if (group == null)
            return false;

        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] != null && !group[i].IsDead)
                return false;
        }

        return true;
    }

    private void RefreshEchoMarks()
    {
        PaintMark(echoFoolMark, echoFoolDone);
        PaintMark(echoChariotMark, echoChariotDone);
        PaintMark(echoStrengthMark, echoStrengthDone);
    }

    private static void PaintMark(Text mark, bool done)
    {
        if (mark == null)
            return;

        mark.text = done ? "完成" : "未完成";
        mark.color = done ? new Color(1f, 0.86f, 0.35f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
    }

    private void RefreshChariotObjective()
    {
        if (cleared || finaleEnding)
        {
            SetObjective("三张牌一起合上了核心");
            return;
        }

        float x = player != null ? player.transform.position.x : 0f;
        if (x > 33f || finaleFoolBroken)
        {
            if (!finaleFoolBroken)
                SetObjective("换上愚者，鼠标左键打过栏杆，打断第一印");
            else if (!finaleLaneClear)
                SetObjective("换上战车，左 Shift 冲刺穿过，靠近核心");
            else
                SetObjective("换上力量，跳起后鼠标左键，砸碎核心");
            return;
        }

        if (loadout == null || loadout.EquippedCard == null)
        {
            SetObjective("左边试单牌。往右按印记换牌，收束核心");
            return;
        }

        switch (loadout.EquippedCard.effectType)
        {
            case CardEffectType.FoolLunge:
                SetObjective(echoFoolDone ? "愚者已完成。往右打第一印" : "站上栏杆前，鼠标左键打过栏杆");
                return;
            case CardEffectType.ChariotDash:
                SetObjective(echoChariotDone ? "战车已完成。往右冲向核心" : "左 Shift 冲刺穿过列兵");
                return;
            case CardEffectType.StrengthSlam:
                SetObjective(echoStrengthDone ? "力量已完成。往右砸碎核心" : "从台上跳起，鼠标左键坠地");
                return;
        }

        SetObjective("触碰印记换牌");
    }
}
