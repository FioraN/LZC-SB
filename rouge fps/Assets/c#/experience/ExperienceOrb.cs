using UnityEngine;

/// <summary>
/// Simple collectible XP orb with optional magnetic pull towards the player.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public sealed class ExperienceOrb : MonoBehaviour
{
    [Min(1)] public int experienceValue = 1;

    [Header("Pickup")]
    public string playerTag = "Player";
    [Min(0f)] public float seekRadius = 3f;
    [Min(0f)] public float moveSpeed = 8f;
    [Min(0f)] public float acceleration = 20f;
    [Min(0f)] public float spawnImpulse = 1.5f;

    private Rigidbody _rb;
    private Transform _target;
    private float _currentSpeed;

    private void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;

        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
            _rb.useGravity = false;
    }

    private void Start()
    {
        if (_rb != null && spawnImpulse > 0f)
        {
            Vector3 impulse = (Random.onUnitSphere + Vector3.up).normalized * spawnImpulse;
            _rb.AddForce(impulse, ForceMode.Impulse);
        }
    }

    private void Update()
    {
        if (_target == null)
            TryFindTarget();

        if (_target == null)
            return;

        float sqrDistance = (_target.position - transform.position).sqrMagnitude;
        if (sqrDistance > seekRadius * seekRadius)
            return;

        _currentSpeed = Mathf.MoveTowards(_currentSpeed, moveSpeed, acceleration * Time.deltaTime);
        Vector3 nextPosition = Vector3.MoveTowards(transform.position, _target.position, _currentSpeed * Time.deltaTime);

        if (_rb != null)
            _rb.MovePosition(nextPosition);
        else
            transform.position = nextPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerExperience receiver = other.GetComponentInParent<PlayerExperience>();
        if (receiver == null)
            return;

        bool matchesTag = string.IsNullOrWhiteSpace(playerTag)
            || other.CompareTag(playerTag)
            || other.transform.root.CompareTag(playerTag);
        if (!matchesTag)
            return;

        receiver.AddExperience(experienceValue);
        Destroy(gameObject);
    }

    private void TryFindTarget()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
            return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
            _target = player.transform;
    }
}
