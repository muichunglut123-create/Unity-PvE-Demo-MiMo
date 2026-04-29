using System;
using UnityEngine;

/// <summary>
/// 子彈（物件池）：負責移動、碰撞/觸發後歸還到池。
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledBullet : MonoBehaviour
{
    [Header("狀態（不建議改）")]
    [Tooltip("用於避免同一顆子彈在同一發期間多次命中歸還。")]
    [SerializeField] private bool isActiveBullet;

    [Header("碰撞與命中判斷")]
    [Tooltip("碰到可受傷目標時，會呼叫其 IDamageable.TakeDamage。")]
    [SerializeField] private bool dealDamageOnHit = true;

    [Tooltip("是否允許子彈傷害玩家。預設關閉，避免打到自己。")]
    [SerializeField] private bool canDamagePlayer = false;

    [Tooltip("是否允許子彈傷害敵人。")]
    [SerializeField] private bool canDamageEnemy = true;

    private Rigidbody _rigidbody;
    private Action<PooledBullet> _returnToPool;

    private Vector3 _direction;
    private float _speed;
    private float _damage;
    private float _expireTime;
    private bool _hasReturned;
    private Transform _ownerRoot;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // 每次重新啟用時由 Gun 呼叫 Fire 才會真正設置參數
        _hasReturned = false;
        isActiveBullet = true;
    }

    private void OnDisable()
    {
        isActiveBullet = false;

        // 如果有 Rigidbody，停掉速度，避免被重複使用時殘留動量。
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void Initialize(Action<PooledBullet> returnToPool)
    {
        _returnToPool = returnToPool;
    }

    /// <summary>
    /// 由槍發射時呼叫。
    /// </summary>
    public void Fire(Vector3 position, Vector3 direction, float speed, float damage, float lifetimeSeconds, Transform ownerRoot)
    {
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward);

        _direction = direction.normalized;
        _speed = Mathf.Max(0f, speed);
        _damage = Mathf.Max(0f, damage);
        _expireTime = Time.time + Mathf.Max(0.01f, lifetimeSeconds);
        _hasReturned = false;
        _ownerRoot = ownerRoot;

        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
            _rigidbody.linearVelocity = _direction * _speed;
        }
    }

    private void Update()
    {
        if (_hasReturned) return;
        if (Time.time >= _expireTime)
        {
            ReturnToPool();
            return;
        }

        // 若沒有 Rigidbody，就用 Transform 手動移動。
        if (_rigidbody == null)
        {
            transform.position += _direction * (_speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasReturned) return;

        TryDealDamage(other);
        ReturnToPool();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasReturned) return;

        TryDealDamage(collision.collider);
        ReturnToPool();
    }

    private void TryDealDamage(Collider other)
    {
        if (!dealDamageOnHit) return;
        if (other == null) return;
        if (_ownerRoot != null && other.transform.IsChildOf(_ownerRoot)) return;

        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        if (!canDamagePlayer)
        {
            var ph = other.GetComponentInParent<PlayerHealth>();
            if (ph != null) return;
        }

        if (!canDamageEnemy)
        {
            var eh = other.GetComponentInParent<EnemyHealth>();
            if (eh != null) return;
        }

        damageable.TakeDamage(_damage);
    }

    private void ReturnToPool()
    {
        if (_hasReturned) return;
        _hasReturned = true;

        if (_returnToPool != null)
        {
            _returnToPool(this);
        }
        else
        {
            // fallback：沒有 returnCallback 就直接停用物件
            gameObject.SetActive(false);
        }
    }
}

