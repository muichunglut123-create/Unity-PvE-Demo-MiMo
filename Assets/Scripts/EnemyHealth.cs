using System;
using UnityEngine;

/// <summary>
/// 敵人生命值：可被子彈扣血，死亡時觸發事件供生成器重生。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("生命值")]
    [SerializeField] private float maxHealth = 30f;

    [Tooltip("死亡後是否 Destroy 物件。若 false 會 SetActive(false)。")]
    [SerializeField] private bool destroyOnDeath = true;

    public event Action<EnemyHealth> Died;
    public static event Action<EnemyHealth> AnyEnemyDied;

    private float _currentHealth;
    private bool _isDead;

    private void Awake()
    {
        _currentHealth = Mathf.Max(0f, maxHealth);
        _isDead = _currentHealth <= 0f;
    }

    public void TakeDamage(float amount)
    {
        if (_isDead) return;
        if (amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    public void ConfigureMaxHealth(float value)
    {
        if (value <= 0f) return;
        maxHealth = value;
        _currentHealth = maxHealth;
        _isDead = false;
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;

        // 先發事件，讓 Spawner 能拿到位置/參考
        Died?.Invoke(this);
        AnyEnemyDied?.Invoke(this);

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

