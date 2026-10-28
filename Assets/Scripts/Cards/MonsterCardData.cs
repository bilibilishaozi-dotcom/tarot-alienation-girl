using UnityEngine;

public enum CardEffectType
{
    FoolLunge = 0,
    ChariotDash = 1,
    StrengthSlam = 2
}

[CreateAssetMenu(fileName = "MonsterCard", menuName = "Tarot/Monster Card")]
public class MonsterCardData : ScriptableObject
{
    public string cardId = "fool";
    public string displayName = "愚者";
    [TextArea] public string description = "攻击会伸得更远，刀光变成金色，伤害变成两倍。";
    public CardEffectType effectType = CardEffectType.FoolLunge;
    public float damageMultiplier = 2f;
    public float lungeMultiplier = 1.75f;
    public float reachMultiplier = 2.15f;
    public int echoDamage = 1;
    public int slamDamage = 3;
}
