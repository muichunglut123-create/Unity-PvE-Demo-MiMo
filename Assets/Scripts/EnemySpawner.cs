using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵人生成器：波次制 PvE（每波生成多隻敵人，清完後進下一波）。
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemySpawner : MonoBehaviour
{
    [Header("生成設定")]
    [Tooltip("敵人預製體（建議包含 EnemyChaserTouchDamage + EnemyHealth + CharacterController + Collider）。")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("生成點。若未指定，使用此 Spawner 自己的位置。")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("每次生成間隔（秒）。")]
    [SerializeField] private float spawnIntervalSeconds = 0.35f;

    [Tooltip("是否隨機選擇生成點。")]
    [SerializeField] private bool randomSpawnPoint = true;

    [Header("波次")]
    [SerializeField] private int startWave = 1;
    [SerializeField] private int maxWave = 8;
    [SerializeField] private int baseEnemiesPerWave = 4;
    [SerializeField] private int extraEnemiesPerWave = 2;
    [SerializeField] private float timeBetweenWaves = 2f;

    [Header("敵人成長")]
    [SerializeField] private float baseEnemyHealth = 30f;
    [SerializeField] private float enemyHealthGrowthPerWave = 8f;
    [SerializeField] private float baseEnemyMoveSpeed = 3.5f;
    [SerializeField] private float enemyMoveSpeedGrowthPerWave = 0.2f;
    [SerializeField] private float baseEnemyTouchDamage = 8f;
    [SerializeField] private float enemyTouchDamageGrowthPerWave = 1.5f;

    public static event System.Action<int, int> WaveChanged;
    public static event System.Action<int> AliveEnemyCountChanged;
    public static event System.Action AllWavesCleared;

    public int CurrentWave => _currentWave;
    public int AliveEnemyCount => _aliveEnemies.Count;

    private readonly HashSet<EnemyHealth> _aliveEnemies = new HashSet<EnemyHealth>();
    private Coroutine _waveRoutine;
    private int _currentWave;
    private int _spawnIndex;

    private void Start()
    {
        _currentWave = Mathf.Max(1, startWave);
        _waveRoutine = StartCoroutine(WaveLoop());
    }

    private void OnDisable()
    {
        foreach (var enemy in _aliveEnemies)
        {
            if (enemy != null) enemy.Died -= OnEnemyDied;
        }
        _aliveEnemies.Clear();
        AliveEnemyCountChanged?.Invoke(0);
    }

    private IEnumerator WaveLoop()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError($"{nameof(EnemySpawner)}：未指定 enemyPrefab。", this);
            yield break;
        }

        int finalWave = Mathf.Max(startWave, maxWave);
        while (_currentWave <= finalWave)
        {
            WaveChanged?.Invoke(_currentWave, finalWave);

            int toSpawn = Mathf.Max(1, baseEnemiesPerWave + (_currentWave - startWave) * extraEnemiesPerWave);
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnOneEnemyForCurrentWave();
                yield return new WaitForSeconds(Mathf.Max(0.01f, spawnIntervalSeconds));
            }

            while (_aliveEnemies.Count > 0)
            {
                yield return null;
            }

            _currentWave++;
            if (_currentWave <= finalWave)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, timeBetweenWaves));
            }
        }

        AllWavesCleared?.Invoke();
    }

    private void SpawnOneEnemyForCurrentWave()
    {
        Transform spawnPoint = ChooseSpawnPoint();
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        var enemy = Instantiate(enemyPrefab, pos, rot);
        var health = enemy.GetComponentInChildren<EnemyHealth>(true);
        var chaser = enemy.GetComponentInChildren<EnemyChaserTouchDamage>(true);

        if (health != null)
        {
            float hp = baseEnemyHealth + (_currentWave - startWave) * enemyHealthGrowthPerWave;
            health.ConfigureMaxHealth(hp);
            health.Died += OnEnemyDied;
            _aliveEnemies.Add(health);
            AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
        }
        else
        {
            Debug.LogWarning($"{nameof(EnemySpawner)}：生成的敵人缺少 {nameof(EnemyHealth)}。", enemy);
        }

        if (chaser != null)
        {
            float speed = baseEnemyMoveSpeed + (_currentWave - startWave) * enemyMoveSpeedGrowthPerWave;
            float damage = baseEnemyTouchDamage + (_currentWave - startWave) * enemyTouchDamageGrowthPerWave;
            chaser.ConfigureStats(speed, damage);
        }
    }

    private void OnEnemyDied(EnemyHealth health)
    {
        if (health == null) return;

        health.Died -= OnEnemyDied;
        if (_aliveEnemies.Remove(health))
        {
            AliveEnemyCountChanged?.Invoke(_aliveEnemies.Count);
        }
    }

    private Transform ChooseSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return transform;
        }

        if (randomSpawnPoint)
        {
            int idx = Random.Range(0, spawnPoints.Length);
            return spawnPoints[idx] != null ? spawnPoints[idx] : transform;
        }

        // 依序循環
        _spawnIndex = (_spawnIndex + 1) % Mathf.Max(1, spawnPoints.Length);
        return spawnPoints[_spawnIndex] != null ? spawnPoints[_spawnIndex] : transform;
    }
}

