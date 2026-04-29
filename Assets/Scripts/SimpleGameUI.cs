using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 簡單遊戲 UI：顯示玩家血量與操作提示。
/// 若未指定 UI 參照，會在執行時自動建立 Canvas 與 Text。
/// </summary>
[DisallowMultipleComponent]
public sealed class SimpleGameUI : MonoBehaviour
{
    [Header("參照")]
    [Tooltip("玩家血量腳本。若未指定，會嘗試以 Tag=Player 尋找。")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("UI（可手動指定，不指定會自動建立）")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private Text healthText;
    [SerializeField] private Text killCountText;
    [SerializeField] private Text waveText;
    [SerializeField] private Text aliveEnemyText;
    [SerializeField] private Text hintText;
    [SerializeField] private Text crosshairText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Text gameOverTitleText;
    [SerializeField] private Text finalKillCountText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button backToMenuButton;

    [Header("樣式")]
    [SerializeField] private int healthFontSize = 28;
    [SerializeField] private int hintFontSize = 18;
    [SerializeField] private int gameOverTitleFontSize = 52;
    [SerializeField] private int restartButtonFontSize = 28;
    [SerializeField] private int finalKillCountFontSize = 30;
    [SerializeField] private Color healthColor = Color.white;
    [SerializeField] private Color killCountColor = new Color(1f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color waveColor = new Color(0.6f, 0.9f, 1f, 1f);
    [SerializeField] private Color hintColor = new Color(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color gameOverPanelColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color gameOverTextColor = Color.white;
    [SerializeField] private Color restartButtonColor = new Color(0.85f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color backToMenuButtonColor = new Color(0.2f, 0.5f, 0.9f, 1f);

    [Header("場景")]
    [Tooltip("主選單場景名稱（需已加入 Build Settings）。")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private int _killCount;
    private bool _subscribedToPlayer;
    private bool _isGameEnded;

    private void Awake()
    {
        // 進場先確保時間是正常流動（避免上一場在 GameOver 暫停後殘留）
        Time.timeScale = 1f;

        TryFindPlayerHealth();
        EnsureUI();
        BindPlayerEventsIfNeeded();
        RefreshHealthText();
        RefreshKillCountText();
        RefreshWaveText(1, 1);
        RefreshAliveEnemyText(0);
        SetHintText();
        SetGameOverVisible(false);
    }

    private void OnEnable()
    {
        BindPlayerEventsIfNeeded();
        EnemyHealth.AnyEnemyDied += OnAnyEnemyDied;
        EnemySpawner.WaveChanged += OnWaveChanged;
        EnemySpawner.AliveEnemyCountChanged += OnAliveEnemyCountChanged;
        EnemySpawner.AllWavesCleared += OnAllWavesCleared;
    }

    private void OnDisable()
    {
        UnbindPlayerEventsIfNeeded();
        EnemyHealth.AnyEnemyDied -= OnAnyEnemyDied;
        EnemySpawner.WaveChanged -= OnWaveChanged;
        EnemySpawner.AliveEnemyCountChanged -= OnAliveEnemyCountChanged;
        EnemySpawner.AllWavesCleared -= OnAllWavesCleared;
    }

    private void Update()
    {
        // 若玩家是在場景後期才生成，做一次補抓。
        if (playerHealth == null)
        {
            TryFindPlayerHealth();
            if (playerHealth != null)
            {
                BindPlayerEventsIfNeeded();
                RefreshHealthText();
            }
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        if (healthText == null) return;
        int c = Mathf.CeilToInt(current);
        int m = Mathf.Max(1, Mathf.CeilToInt(max));
        healthText.text = $"HP: {c} / {m}";
    }

    private void RefreshHealthText()
    {
        if (healthText == null) return;

        if (playerHealth == null)
        {
            healthText.text = "HP: --";
            return;
        }

        OnHealthChanged(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void SetHintText()
    {
        if (hintText == null) return;
        hintText.text = "WASD 移動 | Shift 衝刺 | Space 跳躍 | 滑鼠看向 | 左鍵開火";
    }

    private void OnAnyEnemyDied(EnemyHealth enemy)
    {
        _killCount++;
        RefreshKillCountText();
    }

    private void RefreshKillCountText()
    {
        if (killCountText == null) return;
        killCountText.text = $"Kill Count: {_killCount}";
    }

    private void OnPlayerDied()
    {
        if (_isGameEnded) return;
        _isGameEnded = true;

        if (finalKillCountText != null)
        {
            finalKillCountText.text = $"Final Kill Count: {_killCount}";
        }

        Time.timeScale = 0f;
        SetGameOverVisible(true);
    }

    private void OnWaveChanged(int current, int max)
    {
        RefreshWaveText(current, max);
    }

    private void OnAliveEnemyCountChanged(int alive)
    {
        RefreshAliveEnemyText(alive);
    }

    private void OnAllWavesCleared()
    {
        if (_isGameEnded) return;
        _isGameEnded = true;

        if (gameOverTitleText != null) gameOverTitleText.text = "YOU WIN";
        if (finalKillCountText != null) finalKillCountText.text = $"Final Kill Count: {_killCount}";
        Time.timeScale = 0f;
        SetGameOverVisible(true);
    }

    private void RefreshWaveText(int current, int max)
    {
        if (waveText == null) return;
        waveText.text = $"Wave: {current}/{max}";
    }

    private void RefreshAliveEnemyText(int alive)
    {
        if (aliveEnemyText == null) return;
        aliveEnemyText.text = $"Alive: {alive}";
    }

    private void TryFindPlayerHealth()
    {
        if (playerHealth != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponentInChildren<PlayerHealth>(true);
        }
    }

    private void BindPlayerEventsIfNeeded()
    {
        if (playerHealth == null) return;
        if (_subscribedToPlayer) return;

        playerHealth.HealthChanged += OnHealthChanged;
        playerHealth.Died += OnPlayerDied;
        _subscribedToPlayer = true;
    }

    private void UnbindPlayerEventsIfNeeded()
    {
        if (playerHealth == null) return;
        if (!_subscribedToPlayer) return;

        playerHealth.HealthChanged -= OnHealthChanged;
        playerHealth.Died -= OnPlayerDied;
        _subscribedToPlayer = false;
    }

    private void SetGameOverVisible(bool visible)
    {
        if (gameOverPanel == null) return;
        gameOverPanel.SetActive(visible);
    }

    private void EnsureUI()
    {
        if (rootCanvas == null)
        {
            var canvasGo = new GameObject("SimpleGameUI_Canvas");
            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        if (healthText == null)
        {
            healthText = CreateText("HealthText", rootCanvas.transform, TextAnchor.UpperLeft, healthFontSize, healthColor);
            var rect = healthText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(360f, 60f);
        }

        if (killCountText == null)
        {
            killCountText = CreateText("KillCountText", rootCanvas.transform, TextAnchor.UpperLeft, healthFontSize, killCountColor);
            var rect = killCountText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -62f);
            rect.sizeDelta = new Vector2(360f, 60f);
        }

        if (waveText == null)
        {
            waveText = CreateText("WaveText", rootCanvas.transform, TextAnchor.UpperLeft, healthFontSize, waveColor);
            var rect = waveText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -100f);
            rect.sizeDelta = new Vector2(360f, 60f);
        }

        if (aliveEnemyText == null)
        {
            aliveEnemyText = CreateText("AliveEnemyText", rootCanvas.transform, TextAnchor.UpperLeft, healthFontSize, waveColor);
            var rect = aliveEnemyText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -138f);
            rect.sizeDelta = new Vector2(360f, 60f);
        }

        if (hintText == null)
        {
            hintText = CreateText("HintText", rootCanvas.transform, TextAnchor.LowerRight, hintFontSize, hintColor);
            var rect = hintText.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(760f, 60f);
        }

        if (crosshairText == null)
        {
            crosshairText = CreateText("CrosshairText", rootCanvas.transform, TextAnchor.MiddleCenter, 30, Color.white);
            crosshairText.text = "+";
            var rect = crosshairText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(40f, 40f);
        }

        EnsureGameOverUI();
    }

    private static Text CreateText(string objName, Transform parent, TextAnchor anchor, int size, Color color)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = GetBuiltinFont();
        text.alignment = anchor;
        text.fontSize = size;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        return text;
    }

    private void EnsureGameOverUI()
    {
        if (gameOverPanel == null)
        {
            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(rootCanvas.transform, false);

            var panelRect = gameOverPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var panelImage = gameOverPanel.AddComponent<Image>();
            panelImage.color = gameOverPanelColor;
        }

        if (gameOverTitleText == null)
        {
            gameOverTitleText = CreateText("GameOverTitle", gameOverPanel.transform, TextAnchor.MiddleCenter, gameOverTitleFontSize, gameOverTextColor);
            gameOverTitleText.text = "GAME OVER";
            var titleRect = gameOverTitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 70f);
            titleRect.sizeDelta = new Vector2(800f, 120f);
        }

        if (finalKillCountText == null)
        {
            finalKillCountText = CreateText("FinalKillCountText", gameOverPanel.transform, TextAnchor.MiddleCenter, finalKillCountFontSize, gameOverTextColor);
            finalKillCountText.text = "Final Kill Count: 0";
            var finalRect = finalKillCountText.rectTransform;
            finalRect.anchorMin = new Vector2(0.5f, 0.5f);
            finalRect.anchorMax = new Vector2(0.5f, 0.5f);
            finalRect.pivot = new Vector2(0.5f, 0.5f);
            finalRect.anchoredPosition = new Vector2(0f, 18f);
            finalRect.sizeDelta = new Vector2(900f, 80f);
        }

        if (restartButton == null)
        {
            var btnGo = new GameObject("RestartButton");
            btnGo.transform.SetParent(gameOverPanel.transform, false);

            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0f, -50f);
            btnRect.sizeDelta = new Vector2(240f, 70f);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = restartButtonColor;

            restartButton = btnGo.AddComponent<Button>();
            restartButton.targetGraphic = btnImage;
            restartButton.onClick.AddListener(RestartCurrentScene);

            var label = CreateText("Text", btnGo.transform, TextAnchor.MiddleCenter, restartButtonFontSize, Color.white);
            label.text = "重新開始";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        if (backToMenuButton == null)
        {
            var btnGo = new GameObject("BackToMenuButton");
            btnGo.transform.SetParent(gameOverPanel.transform, false);

            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0f, -132f);
            btnRect.sizeDelta = new Vector2(240f, 70f);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = backToMenuButtonColor;

            backToMenuButton = btnGo.AddComponent<Button>();
            backToMenuButton.targetGraphic = btnImage;
            backToMenuButton.onClick.AddListener(BackToMainMenu);

            var label = CreateText("Text", btnGo.transform, TextAnchor.MiddleCenter, restartButtonFontSize, Color.white);
            label.text = "返回主選單";
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
    }

    private static Font GetBuiltinFont()
    {
        // Unity 新版已不支援 Arial.ttf built-in，改用 LegacyRuntime.ttf
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        _isGameEnded = false;
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void BackToMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning($"{nameof(SimpleGameUI)}：未設定 mainMenuSceneName，無法返回主選單。", this);
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}

