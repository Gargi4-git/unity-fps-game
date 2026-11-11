using UnityEngine;

/// <summary>
/// Walk into it to heal. Put on a prefab, then assign that prefab to
/// Enemy > Drop Prefab (or place it in the level).
/// </summary>
public class HealthPickup : MonoBehaviour
{
    public int healAmount = 25;
    public AudioClip pickupSFX;
    public float spinSpeed = 90f;
    public float bobHeight = 0.1f;
    public float bobSpeed = 2f;

    private Vector3 startPos;

    private void Awake()
    {
        Collider c = GetComponent<Collider>();

        if (c == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.7f;
            c = sc;
        }

        c.isTrigger = true;
    }

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth ph = other.GetComponentInParent<PlayerHealth>();

        if (ph == null || ph.IsDead || ph.health >= ph.maxHealth)
            return;

        ph.Heal(healAmount);
        AudioManager.Instance.PlaySFX(pickupSFX, 0.7f);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowMessage("+" + healAmount + " HEALTH", 1.5f);

        Destroy(gameObject);
    }
}
