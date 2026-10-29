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

    private void OnEnable()
    {
        CardReward.Granted += OnCardGranted;
    }

    private void Start()
    {
        // 从「牌之回响」选牌进 Boss 关时，把三张牌都放进背包，装上选中的那张。
        if (string.IsNullOrEmpty(GameFlow.SelectedCardPath))
            return;

        string[] all = { "Cards/Fool", "Cards/Chariot", "Cards/Strength" };
        for (int i = 0; i < all.Length; i++)
        {
            MonsterCardData owned = Resources.Load<MonsterCardData>(all[i]);
            if (owned != null)
                Remember(owned);
        }

        MonsterCardData chosen = Resources.Load<MonsterCardData>(GameFlow.SelectedCardPath);
        if (chosen != null)
            Equip(chosen, false);
    }

    private void OnDisable()
    {
        CardReward.Granted -= OnCardGranted;
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

    private void Remember(MonsterCardData card)
    {
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null && owned[i].cardId == card.cardId)
                return;
        }

        owned.Add(card);
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
