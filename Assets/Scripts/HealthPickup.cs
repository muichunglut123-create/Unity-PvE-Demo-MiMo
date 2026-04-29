using UnityEngine;

/// <summary>
/// 回血道具：玩家觸碰後回復生命並銷毀。
/// </summary>
[DisallowMultipleComponent]
public sealed class HealthPickup : MonoBehaviour
{
    [SerializeField] private float healAmount = 25f;
    [SerializeField] private float rotateSpeed = 120f;
    [SerializeField] private float lifeTimeSeconds = 15f;

    private void OnEnable()
    {
        if (lifeTimeSeconds > 0f)
        {
            Destroy(gameObject, lifeTimeSeconds);
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        if (health.IsDead) return;

        health.Heal(healAmount);
        Destroy(gameObject);
    }
}

