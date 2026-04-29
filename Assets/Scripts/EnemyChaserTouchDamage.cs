using System.Collections;
using UnityEngine;

/// <summary>
/// 敵人：開始後自動追蹤 Player；敵人觸碰 Player 造成扣血。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyChaserTouchDamage : MonoBehaviour
{
    [Header("追蹤目標")]
    [Tooltip("要追蹤的 Player Transform。若未指定，會嘗試以 Tag=Player 找到。")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("自動追蹤在遊戲開始後啟動延遲（秒）。")]
    [SerializeField] private float chaseStartDelaySeconds = 0.1f;

    [Header("移動/旋轉")]
    [Tooltip("移動速度。")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Tooltip("旋轉速度。")]
    [SerializeField] private float turnSpeed = 720f;

    [Header("扣血")]
    [Tooltip("觸碰造成傷害。")]
    [SerializeField] private float touchDamage = 10f;

    [Tooltip("觸碰扣血冷卻（秒），避免連續每幀扣很多次。")]
    [SerializeField] private float touchDamageCooldownSeconds = 0.5f;

    [Header("需求元件")]
    [Tooltip("敵人建議使用 CharacterController（用來移動）。")]
    [SerializeField] private CharacterController characterController;

    [Tooltip("是否在敵人上自動設 IsTrigger（若碰撞器存在）。")]
    [SerializeField] private bool autoSetTrigger = false;

    private float _nextTouchTime;
    private Vector3 _velocity;
    private bool _isChasing;
    private bool _hasCachedPlayer;
    private Transform _lastKnownPlayer;

    private void Awake()
    {
        if (!characterController)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (autoSetTrigger)
        {
            var coll = GetComponentInChildren<Collider>(true);
            if (coll != null && !coll.isTrigger)
            {
                coll.isTrigger = true;
            }
        }
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            // 僅嘗試一次，避免每幀 Find。
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTransform = go.transform;
        }

        _lastKnownPlayer = playerTransform;
        StartCoroutine(BeginChaseAfterDelay());
    }

    private IEnumerator BeginChaseAfterDelay()
    {
        yield return new WaitForSeconds(chaseStartDelaySeconds);
        _isChasing = true;
    }

    private void Update()
    {
        if (!_isChasing) return;

        if (!playerTransform && !_hasCachedPlayer)
        {
            // 如果剛開始沒找到或玩家在後面才生成，做一次補救。
            _hasCachedPlayer = true;
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTransform = go.transform;
        }

        if (playerTransform == null) return;

        MoveTowardPlayer();
    }

    private void MoveTowardPlayer()
    {
        if (characterController == null)
        {
            // 沒 CharacterController 就不移動（避免錯誤行為）。
            return;
        }

        Vector3 toTarget = playerTransform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        // 水平移動（讓 CharacterController 處理碰撞）
        Vector3 desiredVelocity = transform.forward * moveSpeed;

        // 垂直落下（保持在地面/受重力影響）
        if (characterController.isGrounded && _velocity.y < 0f)
        {
            _velocity.y = -2f;
        }
        else
        {
            _velocity.y += Physics.gravity.y * Time.deltaTime;
        }

        Vector3 move = (desiredVelocity + new Vector3(0f, _velocity.y, 0f)) * Time.deltaTime;
        characterController.Move(move);
    }

    public void ConfigureStats(float newMoveSpeed, float newTouchDamage)
    {
        if (newMoveSpeed > 0f) moveSpeed = newMoveSpeed;
        if (newTouchDamage > 0f) touchDamage = newTouchDamage;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isChasing) return;

        if (Time.time < _nextTouchTime) return;

        // 允許玩家 collider 在子物件
        var damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(touchDamage);
            _nextTouchTime = Time.time + Mathf.Max(0.01f, touchDamageCooldownSeconds);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // 使用 CharacterController 時，OnControllerColliderHit 通常比 OnTriggerEnter 更穩定，
        // 不必依賴 Rigidbody 才能收到碰撞事件。
        if (!_isChasing) return;
        if (Time.time < _nextTouchTime) return;

        var damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(touchDamage);
            _nextTouchTime = Time.time + Mathf.Max(0.01f, touchDamageCooldownSeconds);
        }
    }
}

