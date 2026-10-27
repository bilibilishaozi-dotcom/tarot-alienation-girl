using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCardLoadout : MonoBehaviour
{
    private readonly List<MonsterCardData> owned = new List<MonsterCardData>();

    public MonsterCardData PendingCard { get; private set; }
    public MonsterCardData EquippedCard { get; private set; }
    public IReadOnlyList<MonsterCardData> OwnedCards => owned;
    public event Action Changed;

    private void OnEnable()
    {
        CardReward.Granted += OnCardGranted;
    }

    private void OnDisable()
    {
        CardReward.Granted -= OnCardGranted;
    }

    public float DamageMultiplier => IsFoolEquipped ? EquippedCard.damageMultiplier : 1f;
    public float LungeMultiplier => IsFoolEquipped ? EquippedCard.lungeMultiplier : 1f;
    public float ReachMultiplier => IsFoolEquipped ? EquippedCard.reachMultiplier : 1f;

    private bool IsFoolEquipped => EquippedCard != null && EquippedCard.effectType == CardEffectType.FoolLunge;

    public void EquipPending()
    {
        if (PendingCard == null)
            return;

        EquippedCard = PendingCard;
        PendingCard = null;
        Changed?.Invoke();
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
