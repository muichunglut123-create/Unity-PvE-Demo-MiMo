using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 槍：使用物件池發射子彈（Gun & Bullet Object Pool 技術）。
/// </summary>
[DisallowMultipleComponent]
public sealed class GunObjectPool : MonoBehaviour
{
    [Header("參照")]
    [Tooltip("子彈預製體（必須帶有 PooledBullet 腳本）。")]
    [SerializeField] private PooledBullet bulletPrefab;

    [Tooltip("槍口位置（子彈會從這裡出射）。若未指定，預設用自己的 Transform。")]
    [SerializeField] private Transform muzzleTransform;

    [Tooltip("子彈建立後會放在這個父物件下（方便整理層級）。")]
    [SerializeField] private Transform bulletPoolParent;

    [Header("發射設定")]
    [Tooltip("每秒發射次數。")]
    [SerializeField] private float fireRate = 8f;

    [Tooltip("子彈飛行速度。")]
    [SerializeField] private float bulletSpeed = 20f;

    [Tooltip("子彈傷害。")]
    [SerializeField] private float bulletDamage = 10f;

    [Tooltip("子彈存在時間（超過就歸還物件池）。")]
    [SerializeField] private float bulletLifetimeSeconds = 2f;

    [Header("物件池")]
    [Tooltip("初始池大小（建議 20 以上）。")]
    [SerializeField] private int poolSize = 30;

    [Tooltip("子彈池用盡時，是否允許即時計算新增。（true：池用盡會擴充；false：用盡就不再發射）")]
    [SerializeField] private bool allowPoolExpand = true;

    [Header("Input（新 Input System）")]
    [Tooltip("開火按鈕（建議綁 Player/Attack）。")]
    [SerializeField] private InputActionReference fireAction;

    [Tooltip("是否按住就連發（若火力動作是 Button，建議 true）。若 false，只在 performed 時射一次。")]
    [SerializeField] private bool autoFireWhileHeld = true;

    [Tooltip("子彈擁有者（用於避免打到自己）。若未指定，預設用此物件根節點。")]
    [SerializeField] private Transform bulletOwnerRoot;

    [Header("瞄準（畫面中心）")]
    [Tooltip("用於從畫面中心發射射線的相機。若未指定會使用 Camera.main。")]
    [SerializeField] private Camera aimCamera;

    [Tooltip("中心射線最大距離（射擊遊戲常用 200~1000）。")]
    [SerializeField] private float aimRayDistance = 500f;

    [Tooltip("可被中心射線命中的圖層。")]
    [SerializeField] private LayerMask aimHitMask = ~0;

    [Tooltip("是否忽略 Trigger（建議開啟，避免準心被觸發器吸附）。")]
    [SerializeField] private QueryTriggerInteraction aimIgnoreTrigger = QueryTriggerInteraction.Ignore;

    private readonly Queue<PooledBullet> _poolQueue = new Queue<PooledBullet>();
    private float _nextFireTime;
    private bool _fireHeld;

    private void Awake()
    {
        if (muzzleTransform == null) muzzleTransform = transform;
        if (bulletOwnerRoot == null) bulletOwnerRoot = transform.root;
        if (aimCamera == null) aimCamera = Camera.main;

        if (bulletPoolParent == null)
        {
            // 不在層級亂放：自動建立一個父物件
            var go = new GameObject("BulletPool");
            go.transform.SetParent(transform, false);
            bulletPoolParent = go.transform;
        }

        if (bulletPrefab == null)
        {
            Debug.LogError($"{nameof(GunObjectPool)}：缺少 bulletPrefab（子彈預製體）。", this);
            enabled = false;
            return;
        }

        BuildInitialPool();
    }

    private void OnEnable()
    {
        if (fireAction != null && fireAction.action != null)
        {
            if (autoFireWhileHeld)
            {
                fireAction.action.performed += OnFirePerformed;
                fireAction.action.canceled += OnFireCanceled;
            }
            else
            {
                fireAction.action.performed += OnFireOncePerformed;
            }
            EnableAction(fireAction);
        }
    }

    private void OnDisable()
    {
        if (fireAction != null && fireAction.action != null)
        {
            fireAction.action.performed -= OnFirePerformed;
            fireAction.action.canceled -= OnFireCanceled;
            fireAction.action.performed -= OnFireOncePerformed;
        }
    }

    private void Update()
    {
        if (!autoFireWhileHeld) return;
        if (!_fireHeld) return;
        TryFire();
    }

    private void TryFire()
    {
        if (Time.time < _nextFireTime) return;
        if (!TryGetBullet(out var bullet))
        {
            return;
        }

        _nextFireTime = Time.time + (fireRate <= 0f ? 0.25f : (1f / fireRate));

        Vector3 spawnPos = muzzleTransform.position;
        Vector3 direction = CalculateShootDirection(spawnPos);

        bullet.gameObject.SetActive(true);
        bullet.Fire(spawnPos, direction, bulletSpeed, bulletDamage, bulletLifetimeSeconds, bulletOwnerRoot);
    }

    private Vector3 CalculateShootDirection(Vector3 spawnPos)
    {
        // 無相機時，退回原本槍口正前方。
        if (aimCamera == null)
        {
            return muzzleTransform.forward;
        }

        Ray centerRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = centerRay.origin + centerRay.direction * Mathf.Max(1f, aimRayDistance);

        // 使用 RaycastAll，跳過自己角色，避免準心打到自己碰撞器造成偏移。
        RaycastHit[] hits = Physics.RaycastAll(centerRay, Mathf.Max(1f, aimRayDistance), aimHitMask, aimIgnoreTrigger);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitRoot = hits[i].collider.transform.root;
                if (bulletOwnerRoot != null && hitRoot == bulletOwnerRoot)
                {
                    continue;
                }

                targetPoint = hits[i].point;
                break;
            }
        }

        Vector3 dir = targetPoint - spawnPos;
        if (dir.sqrMagnitude < 0.0001f)
        {
            return muzzleTransform.forward;
        }

        return dir.normalized;
    }

    private bool TryGetBullet(out PooledBullet bullet)
    {
        if (_poolQueue.Count > 0)
        {
            bullet = _poolQueue.Dequeue();
            return true;
        }

        if (!allowPoolExpand)
        {
            bullet = null;
            return false;
        }

        bullet = CreateOneBullet();
        return bullet != null;
    }

    private void BuildInitialPool()
    {
        _poolQueue.Clear();

        int count = Mathf.Max(1, poolSize);
        for (int i = 0; i < count; i++)
        {
            var b = CreateOneBullet();
            if (b == null) continue;
            b.gameObject.SetActive(false);
            _poolQueue.Enqueue(b);
        }
    }

    private PooledBullet CreateOneBullet()
    {
        var b = Instantiate(bulletPrefab, bulletPoolParent);
        b.Initialize(ReturnBulletToPool);
        return b;
    }

    private void ReturnBulletToPool(PooledBullet bullet)
    {
        if (bullet == null) return;
        bullet.gameObject.SetActive(false);
        _poolQueue.Enqueue(bullet);
    }

    private void EnableAction(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return;
        if (!actionRef.action.enabled) actionRef.action.Enable();
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        _fireHeld = true;
        TryFire();
    }

    private void OnFireCanceled(InputAction.CallbackContext context)
    {
        _fireHeld = false;
    }

    private void OnFireOncePerformed(InputAction.CallbackContext context)
    {
        _fireHeld = false;
        TryFire();
    }
}

