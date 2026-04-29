using UnityEngine;

/// <summary>
/// 玩家血量與受傷處理（供子彈/敵人呼叫）。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("生命值")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float damageInvincibilitySeconds = 0.2f;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => _currentHealth <= 0f;

    public event System.Action<float, float> HealthChanged;
    public event System.Action Died;

    private float _currentHealth;
    private float _nextDamageTime;

    private void Awake()
    {
        _currentHealth = Mathf.Max(0f, maxHealth);
        HealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (_currentHealth <= 0f) return;
        if (Time.time < _nextDamageTime) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        _nextDamageTime = Time.time + Mathf.Max(0f, damageInvincibilitySeconds);
        HealthChanged?.Invoke(_currentHealth, maxHealth);
        if (_currentHealth <= 0f)
        {
            OnDeath();
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        if (_currentHealth <= 0f) return;

        _currentHealth = Mathf.Min(maxHealth, _currentHealth + amount);
        HealthChanged?.Invoke(_currentHealth, maxHealth);
    }

    private void OnDeath()
    {
        Died?.Invoke();

        if (destroyOnDeath)
        {
            Destroy(gameObject);
            return;
        }

        // 簡單做法：停用物件，避免繼續被其他碰撞/射擊扣血。
        gameObject.SetActive(false);
    }
}

