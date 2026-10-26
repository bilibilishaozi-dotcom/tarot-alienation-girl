using UnityEngine;

public class TrainingEncounter : MonoBehaviour
{
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private Vector2 firstOffset = new Vector2(6f, 1.5f);
    [SerializeField] private Vector2 secondOffset = new Vector2(12f, 1.5f);
    [SerializeField] private int firstHealth = 3;
    [SerializeField] private int secondHealth = 4;

    private void Start()
    {
        if (!spawnOnStart)
            return;

        Spawn("BasicEnemy_Spore", firstOffset, firstHealth, true, "孢子");
        Spawn("BasicEnemy_Spore_2", secondOffset, secondHealth, false, "孢子");
    }

    private void Spawn(string enemyName, Vector2 offset, int health, bool dropsFoolCard, string displayName)
    {
        GameObject enemy = new GameObject(enemyName);
        enemy.transform.position = transform.position + (Vector3)offset;
        enemy.AddComponent<SpriteRenderer>();
        enemy.AddComponent<Rigidbody2D>();
        CapsuleCollider2D capsule = enemy.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(0.8f, 1.5f);
        enemy.AddComponent<BasicEnemy>().Configure(health, dropsFoolCard, GetComponent<Player>(), displayName);
    }
}
