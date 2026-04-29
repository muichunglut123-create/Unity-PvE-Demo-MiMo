using UnityEngine;

/// <summary>
/// 敵人掉落：死亡時機率生成回血道具。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyLootDrop : MonoBehaviour
{
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private GameObject healthPickupPrefab;
    [SerializeField] [Range(0f, 1f)] private float healthDropChance = 0.25f;
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.25f, 0f);

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died += OnEnemyDied;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= OnEnemyDied;
        }
    }

    private void OnEnemyDied(EnemyHealth health)
    {
        if (healthPickupPrefab == null) return;
        if (Random.value > healthDropChance) return;

        Vector3 pos = transform.position + dropOffset;
        Instantiate(healthPickupPrefab, pos, Quaternion.identity);
    }
}

