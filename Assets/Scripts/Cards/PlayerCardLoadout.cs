using System;
using UnityEngine;

public class PlayerCardLoadout : MonoBehaviour
{
    public MonsterCardData PendingCard { get; private set; }
    public MonsterCardData EquippedCard { get; private set; }
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

        if (EquippedCard != null && EquippedCard.cardId == card.cardId)
            return;

        PendingCard = card;
        Changed?.Invoke();
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
