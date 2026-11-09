using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCardLoadout : MonoBehaviour
{
    private readonly List<MonsterCardData> owned = new List<MonsterCardData>();
    private ICardEffect active;

    public MonsterCardData PendingCard { get; private set; }
    public MonsterCardData EquippedCard { get; private set; }
    public IReadOnlyList<MonsterCardData> OwnedCards => owned;
    public event Action Changed;

    public float DamageMultiplier { get; private set; } = 1f;
    public float LungeMultiplier { get; private set; } = 1f;
    public float ReachMultiplier { get; private set; } = 1f;
    public bool DashEcho { get; private set; }
    public int EchoDamage { get; private set; }
    public bool LandingSlam { get; private set; }
    public int SlamDamage { get; private set; }

    private string rememberedLevel;

    private void OnEnable()
    {
        CardReward.Granted += OnCardGranted;
        rememberedLevel = GameFlow.ActiveLevelId;
    }

    // 重开本关时把刚才装备的牌装回来（不播演出）。换关则由 GameFlow 丢掉这份记录。
    public void RestoreIfSameLevel()
    {
        if (!GameFlow.KeepLoadoutOnNextLoad || CardRunState.LevelId != rememberedLevel)
        {
            if (!GameFlow.KeepLoadoutOnNextLoad)
                CardRunState.Clear();
            GameFlow.ConsumeLoadoutKeep();
            return;
        }

        GameFlow.ConsumeLoadoutKeep();
        CardRunState.Apply(this);
    }

    // 把愚者/战车/力量三张都放进背包（终局关开场用）。
    public void GrantAllCards()
    {
        string[] all = { "Cards/Fool", "Cards/Chariot", "Cards/Strength" };
        for (int i = 0; i < all.Length; i++)
        {
            MonsterCardData owned = Resources.Load<MonsterCardData>(all[i]);
            if (owned != null)
                Remember(owned);
        }
    }

    public bool EquipFromPath(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return false;

        MonsterCardData card = Resources.Load<MonsterCardData>(resourcePath);
        if (card == null)
            return false;

        Equip(card, false);
        return true;
    }

    // 按背包里的序号切换卡牌（状态栏的 1/2/3）。
    public bool EquipByIndex(int index)
    {
        if (index < 0 || index >= owned.Count)
            return false;

        MonsterCardData card = owned[index];
        if (card == null)
            return false;

        Equip(card, false);
        return true;
    }

    private void OnDisable()
    {
        CardReward.Granted -= OnCardGranted;
        if (GameFlow.KeepLoadoutOnNextLoad)
        {
            // 空的多余组件关掉时，不要盖掉已经记下的背包。
            bool hasCards = owned.Count > 0 || EquippedCard != null;
            if (hasCards || CardRunState.LevelId != rememberedLevel)
                CardRunState.Save(rememberedLevel, this);
        }
        else
            CardRunState.Clear();
        DetachActive();
    }

    public void Equip(MonsterCardData card, bool playEffect = true)
    {
        if (card == null)
            return;

        Remember(card);
        if (EquippedCard != null && EquippedCard.cardId == card.cardId)
        {
            if (PendingCard != null)
            {
                PendingCard = null;
                Changed?.Invoke();
            }

            return;
        }

        DetachActive();
        EquippedCard = card;
        PendingCard = null;
        Player player = GetComponent<Player>();
        active = CardEffects.Create(card.effectType);
        if (active != null && player != null)
            active.Attach(player, card);
        // 身后那圈主题光跟着当前卡牌走
        CardAura.Apply(player, card);
        Changed?.Invoke();
        if (playEffect)
            CardPossession.Play(player, card);
    }

    public void EquipPending()
    {
        if (PendingCard == null)
            return;

        Equip(PendingCard);
    }

    public void ApplyFool(float damage, float lunge, float reach)
    {
        DamageMultiplier = Mathf.Max(0.1f, damage);
        LungeMultiplier = Mathf.Max(0.1f, lunge);
        ReachMultiplier = Mathf.Max(0.1f, reach);
    }

    public void ClearFool()
    {
        DamageMultiplier = 1f;
        LungeMultiplier = 1f;
        ReachMultiplier = 1f;
    }

    public void ApplyChariot(int echoDamage)
    {
        DashEcho = true;
        EchoDamage = Mathf.Max(1, echoDamage);
    }

    public void ClearChariot()
    {
        DashEcho = false;
        EchoDamage = 0;
    }

    public void ApplyStrength(int slamDamage)
    {
        LandingSlam = true;
        SlamDamage = Mathf.Max(1, slamDamage);
    }

    public void ClearStrength()
    {
        LandingSlam = false;
        SlamDamage = 0;
    }

    private void DetachActive()
    {
        Player player = GetComponent<Player>();
        if (active != null && player != null)
            active.Detach(player);
        active = null;
        ClearFool();
        ClearChariot();
        ClearStrength();
        CardAura.Apply(player, null);
    }

    private void OnCardGranted(MonsterCardData card)
    {
        if (card == null)
            return;

        Remember(card);
        if (EquippedCard != null && EquippedCard.cardId == card.cardId)
            return;

        PendingCard = card;
        Changed?.Invoke();
    }

    public void Remember(MonsterCardData card)
    {
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && owned[i].cardId == card.cardId)
                return;
        }

        owned.Add(card);
    }
}

static class CardRunState
{
    public static string LevelId;
    static string equippedName;
    static readonly List<string> ownedNames = new List<string>();

    public static void Clear()
    {
        LevelId = null;
        equippedName = null;
        ownedNames.Clear();
    }

    public static void Save(string levelId, PlayerCardLoadout loadout)
    {
        LevelId = levelId;
        equippedName = loadout.EquippedCard != null ? loadout.EquippedCard.name : null;
        ownedNames.Clear();
        IReadOnlyList<MonsterCardData> cards = loadout.OwnedCards;
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                ownedNames.Add(cards[i].name);
        }
    }

    public static void Apply(PlayerCardLoadout loadout)
    {
        for (int i = 0; i < ownedNames.Count; i++)
        {
            MonsterCardData card = Resources.Load<MonsterCardData>("Cards/" + ownedNames[i]);
            if (card != null)
                loadout.Remember(card);
        }

        if (string.IsNullOrEmpty(equippedName))
            return;

        MonsterCardData equipped = Resources.Load<MonsterCardData>("Cards/" + equippedName);
        if (equipped != null)
            loadout.Equip(equipped, false);
    }
}

public static class CardReward
{
    public static event Action<MonsterCardData> Granted;

    public static void Grant(MonsterCardData card)
    {
        Granted?.Invoke(card);
    }
}
