using System;
using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int amount, Vector2 hitFrom);
}

public interface IHudTarget
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    string DisplayName { get; }
    bool IsDead { get; }
    Vector3 HudWorldPoint { get; }
    event Action HealthChanged;
}
