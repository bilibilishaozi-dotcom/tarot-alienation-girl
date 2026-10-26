using UnityEngine;

public enum CardEffectType
{
    FoolLunge = 0
}

[CreateAssetMenu(fileName = "MonsterCard", menuName = "Tarot/Monster Card")]
public class MonsterCardData : ScriptableObject
{
    public string cardId = "fool";
    public string displayName = "愚者";
    [TextArea] public string description = "攻击变为更长的突进斩，并提高伤害。";
    public CardEffectType effectType = CardEffectType.FoolLunge;
    public float damageMultiplier = 2f;
    public float lungeMultiplier = 1.75f;
    public float reachMultiplier = 1.65f;
}
