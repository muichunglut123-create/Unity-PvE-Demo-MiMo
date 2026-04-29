using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3D 玩家控制器：CharacterController + 新 Input System（WASD 移動 / 滑鼠看向 / 空格跳躍 / Shift 衝刺）
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerController3D : MonoBehaviour
{
    [Header("參照")]
    [Tooltip("CharacterController 元件（建議放在同一個物件上）。")]
    [SerializeField] private CharacterController characterController;

    [Tooltip("用來做俯仰（上下看）的相機 Transform。若未指定，會嘗試尋找子物件中的 Camera。")]
    [SerializeField] private Transform cameraTransform;

    [Header("移動速度")]
    [Tooltip("行走速度（WASD）。")]
    [SerializeField] private float walkSpeed = 5f;

    [Tooltip("衝刺速度倍率（按住 Shift）。")]
    [SerializeField] private float sprintMultiplier = 1.5f;

    [Tooltip("地面加速度（越大轉向越快）。")]
    [SerializeField] private float groundAcceleration = 25f;

    [Tooltip("空中加速度（越大空中轉向越快）。")]
    [SerializeField] private float airAcceleration = 10f;

    [Header("跳躍與重力")]
    [Tooltip("可調整跳躍高度（單位：公尺）。用於計算初速度。")]
    [SerializeField] private float jumpHeight = 2f;

    [Tooltip("最多連續跳躍次數（支援二段跳：建議填 2）。在空中按下跳躍直到次數用完。")]
    [SerializeField] private int maxJumpCount = 2;

    [Tooltip("重力加速度（越大下墜越快）。請使用正值。")]
    [SerializeField] private float gravity = 20f;

    [Tooltip("角色落地後往下的微小黏地力（避免浮在地面上）。")]
    [SerializeField] private float groundedDownForce = 2f;

    [Tooltip("最大下落速度（避免數值過大）。")]
    [SerializeField] private float maxFallSpeed = 50f;

    [Header("看向（滑鼠）")]
    [Tooltip("滑鼠敏感度。Look Input 建議是 Mouse Delta（以新 Input System 的 Delta 形式）。")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("是否反轉 Y 軸（把滑鼠往下拉視為往上看）。")]
    [SerializeField] private bool invertY = false;

    [Tooltip("相機俯仰角最大值。")]
    [SerializeField] private float pitchClampDegrees = 80f;

    [Header("游標鎖定（可選）")]
    [Tooltip("啟用後會在開始時鎖定游標並隱藏，方便第一人稱操作。")]
    [SerializeField] private bool lockCursor = true;

    [Header("衝刺粒子（可選）")]
    [Tooltip("衝刺時播放/停止的粒子特效。")]
    [SerializeField] private ParticleSystem sprintParticleSystem;

    [Tooltip("若未指定 sprintParticleSystem，是否自動建立一個簡易 ParticleSystem（僅在需要時建）。")]
    [SerializeField] private bool autoCreateSprintParticleSystem = false;

    [Header("Input Actions（新 Input System）")]
    [Tooltip("Move：Value Type 建議為 Vector2（X=水平，Y=垂直，對應 WASD）。")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("Look：Value Type 建議為 Vector2（X=滑鼠左右，Y=滑鼠上下，通常是 Delta）。")]
    [SerializeField] private InputActionReference lookAction;

    [Tooltip("Jump：Button（按下空格跳躍）。")]
    [SerializeField] private InputActionReference jumpAction;

    [Tooltip("Sprint：Button（按住 Shift 衝刺）。")]
    [SerializeField] private InputActionReference sprintAction;

    private Vector2 _moveInput;
    private Vector2 _lookInput;

    private float _yaw;
    private float _pitch;

    private Vector3 _velocity; // y 為垂直速度，x/z 為水平速度
    private bool _sprintHeld;
    private int _jumpCount; // 已用跳躍次數（0=尚未跳，1=第一次跳後...）
    private bool _wasGrounded;

    private void Awake()
    {
        if (!characterController)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (cameraTransform == null)
        {
            // 僅做一次嘗試，避免 Awake/Start 重複 Find。
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cameraTransform = cam.transform;
        }

        if (cameraTransform == null)
        {
            // 沒有相機也能控制移動/轉向，只是俯仰不會作用到相機。
            // 由於不是致命錯誤，因此只給警告。
            Debug.LogWarning($"{nameof(PlayerController3D)}：未指定 cameraTransform（或子物件未找到 Camera），俯仰看向將不生效。", this);
        }

        if (characterController == null)
        {
            Debug.LogError($"{nameof(PlayerController3D)}：缺少 CharacterController 元件。", this);
            enabled = false;
            return;
        }

        _wasGrounded = characterController.isGrounded;
        _jumpCount = 0;

        TryEnsureSprintParticles();

        ValidateInputAction(moveAction, nameof(moveAction));
        ValidateInputAction(lookAction, nameof(lookAction));
        ValidateInputAction(jumpAction, nameof(jumpAction));
        ValidateInputAction(sprintAction, nameof(sprintAction));

        _yaw = transform.eulerAngles.y;
        _pitch = cameraTransform != null ? cameraTransform.localEulerAngles.x : 0f;
        _pitch = NormalizeAngleToSignedDegrees(_pitch);
    }

    private void OnEnable()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        EnableAction(moveAction);
        EnableAction(lookAction);
        EnableAction(jumpAction);
        EnableAction(sprintAction);

        if (jumpAction != null)
        {
            jumpAction.action.performed += OnJumpPerformed;
        }

        if (sprintAction != null)
        {
            sprintAction.action.performed += OnSprintPerformed;
            sprintAction.action.canceled += OnSprintCanceled;
        }
    }

    private void OnDisable()
    {
        if (jumpAction != null)
        {
            jumpAction.action.performed -= OnJumpPerformed;
        }

        if (sprintAction != null)
        {
            sprintAction.action.performed -= OnSprintPerformed;
            sprintAction.action.canceled -= OnSprintCanceled;
        }
    }

    private void Update()
    {
        // Input 讀取放在 Update，但僅做輕量的 Value Read（不做昂貴 Find/查表）。
        if (moveAction != null) _moveInput = moveAction.action.ReadValue<Vector2>();
        if (lookAction != null) _lookInput = lookAction.action.ReadValue<Vector2>();

        HandleLook();
        HandleJumpAndGravity();
        HandleMove();
        ApplyMovement();

        // 確保粒子狀態與衝刺狀態一致（避免事件漏觸時看起來不同步）
        UpdateSprintParticles();
    }

    private void HandleLook()
    {
        // 假設 Look 是 Delta（滑鼠移動量），不需要再乘以 deltaTime；敏感度直接用 mouseSensitivity。
        float yawDelta = _lookInput.x * mouseSensitivity;
        float pitchDelta = _lookInput.y * mouseSensitivity;
        if (invertY) pitchDelta = -pitchDelta;

        _yaw += yawDelta;
        _pitch -= pitchDelta; // 常見 FPS：滑鼠往下看，pitch 會增加/減少方向相反，所以用 -=
        _pitch = Mathf.Clamp(_pitch, -pitchClampDegrees, pitchClampDegrees);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (cameraTransform != null)
        {
            // 相機只做俯仰，不改 roll/yaw。
            cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    private void HandleJumpAndGravity()
    {
        // 角色在地面時套用黏地力，讓它更穩定貼地。
        if (characterController.isGrounded)
        {
            // 只有在「剛落地」的瞬間重置跳躍次數，避免每幀重置造成邏輯混亂。
            if (!_wasGrounded) _jumpCount = 0;
            _wasGrounded = true;

            if (_velocity.y < 0f)
            {
                _velocity.y = -groundedDownForce;
            }
        }
        else
        {
            _wasGrounded = false;
            _velocity.y -= gravity * Time.deltaTime;
            _velocity.y = Mathf.Max(_velocity.y, -maxFallSpeed);
        }
    }

    private void HandleMove()
    {
        // 取得水平輸入（X=左右，Y=前後）
        Vector2 input2D = _moveInput;
        if (input2D.sqrMagnitude > 1f) input2D.Normalize();

        // 將輸入轉成以角色朝向為基準的世界移動方向
        Vector3 desiredMove =
            (transform.right * input2D.x) +
            (transform.forward * input2D.y);

        float speed = walkSpeed * (_sprintHeld ? sprintMultiplier : 1f);
        desiredMove *= speed;

        // x/z 水平速度使用不同加速度：地面更快、空中更慢
        Vector3 currentHorizontal = new Vector3(_velocity.x, 0f, _velocity.z);
        Vector3 targetHorizontal = desiredMove;

        float accel = characterController.isGrounded ? groundAcceleration : airAcceleration;
        Vector3 newHorizontal = Vector3.Lerp(currentHorizontal, targetHorizontal, 1f - Mathf.Exp(-accel * Time.deltaTime));

        _velocity.x = newHorizontal.x;
        _velocity.z = newHorizontal.z;
    }

    private void ApplyMovement()
    {
        Vector3 delta = _velocity * Time.deltaTime;
        characterController.Move(delta);
    }

    private float CalculateJumpVelocityY()
    {
        // v = sqrt(2 * g * h)
        return Mathf.Sqrt(2f * gravity * jumpHeight);
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (maxJumpCount < 1) return;

        // 地面：第一次跳
        if (characterController.isGrounded)
        {
            _jumpCount = 1;
            _velocity.y = CalculateJumpVelocityY();
            return;
        }

        // 空中：支援二段跳/多段跳
        if (_jumpCount < maxJumpCount)
        {
            _jumpCount++;
            _velocity.y = CalculateJumpVelocityY();
        }
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        _sprintHeld = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        _sprintHeld = false;
    }

    private void TryEnsureSprintParticles()
    {
        if (sprintParticleSystem != null) return;
        if (!autoCreateSprintParticleSystem) return;

        // 只在需要時建立，讓你不需要先手動掛載 ParticleSystem。
        var go = new GameObject("SprintParticles");
        go.transform.SetParent(transform, false);
        sprintParticleSystem = go.AddComponent<ParticleSystem>();

        var main = sprintParticleSystem.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 0.35f;
        main.startSpeed = 0.9f;
        main.startSize = 0.12f;

        // 讓粒子從身體後方向前噴（簡易視覺效果）
        go.transform.localPosition = new Vector3(0f, 0.1f, -0.2f);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var emission = sprintParticleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 120f;

        var shape = sprintParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.05f;
        shape.length = 0.1f;

        // 預先停止，等待衝刺時 UpdateSprintParticles() 才會播放
        sprintParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateSprintParticles()
    {
        if (sprintParticleSystem == null) return;

        if (_sprintHeld)
        {
            if (!sprintParticleSystem.isPlaying)
            {
                sprintParticleSystem.Play(true);
            }
        }
        else
        {
            if (sprintParticleSystem.isPlaying)
            {
                sprintParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void EnableAction(InputActionReference actionRef)
    {
        if (actionRef == null) return;

        // 某些情況下 InputActionReference.action 可能尚未設定，這裡只做保護。
        var action = actionRef.action;
        if (action == null) return;

        if (!action.enabled)
        {
            action.Enable();
        }
    }

    private static void ValidateInputAction(InputActionReference actionRef, string fieldName)
    {
        if (actionRef == null || actionRef.action == null)
        {
            Debug.LogWarning($"（建議）未指定 {fieldName}，對應功能將無法使用。", null);
            return;
        }
    }

    private static float NormalizeAngleToSignedDegrees(float angle)
    {
        // 把 0~360 的角度轉成 -180~180，方便 clamp。
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}

