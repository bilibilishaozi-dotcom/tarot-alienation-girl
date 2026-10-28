using UnityEngine;

// 关卡运行时生成的触发器：拾取、出口、收束目标和换牌印记。
public class CardPickup : MonoBehaviour
{
    private MonsterCardData card;
    private Vector3 origin;
    private bool taken;

    public void Arm(MonsterCardData data)
    {
        card = data;
        origin = transform.position;
    }

    private void Update()
    {
        if (taken)
            return;

        float wave = Mathf.Sin(Time.time * 2.2f) * 0.1f;
        transform.position = origin + Vector3.up * wave;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || card == null || other.GetComponent<Player>() == null)
            return;

        taken = true;
        CardReward.Grant(card);
        gameObject.SetActive(false);
    }
}

public class LevelExit : MonoBehaviour
{
    private FirstLevel level;
    private bool used;

    public void Arm(FirstLevel owner)
    {
        level = owner;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used || level == null || other.GetComponent<Player>() == null)
            return;

        if (!level.UseExit())
            return;

        used = true;
    }
}

public class FinaleTarget : MonoBehaviour, IDamageable
{
    private int health = 1;
    private bool broken;
    private UnityEngine.Events.UnityAction brokenCallback;

    public void Arm(int amount, UnityEngine.Events.UnityAction onBroken)
    {
        health = Mathf.Max(1, amount);
        brokenCallback = onBroken;
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (broken || amount <= 0)
            return;

        health -= amount;
        if (health > 0)
            return;

        broken = true;
        Collider2D body = GetComponent<Collider2D>();
        if (body != null)
            body.enabled = false;
        AttackFeedback.Thud(transform.position);
        if (brokenCallback != null)
            brokenCallback.Invoke();
    }
}

public class CardSeal : MonoBehaviour
{
    private MonsterCardData card;

    public void Arm(MonsterCardData data)
    {
        card = data;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponent<Player>();
        if (player == null || card == null)
            return;

        PlayerCardLoadout loadout = player.GetComponent<PlayerCardLoadout>();
        if (loadout == null)
            return;

        loadout.Equip(card);
    }
}
