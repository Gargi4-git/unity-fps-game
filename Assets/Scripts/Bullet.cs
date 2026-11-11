using UnityEngine;

/// <summary>
/// Projectile. Deals damage itself (no tags needed):
/// player bullets hurt enemies, enemy bullets hurt the player.
/// </summary>
public class Bullet : MonoBehaviour
{
    public float speed = 30f;
    public float lifeTime = 3f;
    public int damage = 10;
    public GameObject impactEffect;

    private Rigidbody rb;
    private bool ownerIsPlayer;
    private Transform ownerRoot;
    private bool hasHit;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (rb == null)
        {
            Debug.LogError("Bullet needs a Rigidbody!", this);
            return;
        }

        rb.useGravity = false;
        rb.isKinematic = false;

        // Stops fast bullets tunnelling through thin colliders.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        GameUtil.SetVelocity(rb, transform.forward * speed);

        Destroy(gameObject, lifeTime);
    }

    public void SetOwner(GameObject owner)
    {
        if (owner == null)
            return;

        ownerRoot = owner.transform;
        ownerIsPlayer =
            owner.GetComponentInParent<PlayerHealth>() != null ||
            owner.GetComponentInChildren<PlayerHealth>() != null;

        Collider[] myColliders = GetComponentsInChildren<Collider>();
        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>();

        foreach (Collider mine in myColliders)
        {
            if (mine == null || !mine.enabled) continue;

            foreach (Collider theirs in ownerColliders)
            {
                if (theirs == null || !theirs.enabled) continue;
                Physics.IgnoreCollision(mine, theirs);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        HandleHit(collision.collider, point);
    }

    // In case the bullet prefab uses a trigger collider.
    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger)
            return;

        if (ownerRoot != null && other.transform.IsChildOf(ownerRoot))
            return;

        HandleHit(other, transform.position);
    }

    private void HandleHit(Collider col, Vector3 point)
    {
        if (hasHit)
            return;

        hasHit = true;

        IDamageable target = col.GetComponentInParent<IDamageable>();

        if (target != null && !target.IsDead)
        {
            bool targetIsPlayer = target is PlayerHealth;

            // No friendly fire: player->enemy and enemy->player only.
            if (targetIsPlayer != ownerIsPlayer)
            {
                target.TakeDamage(damage);

                if (ownerIsPlayer)
                {
                    GameManager.NotifyShotHit();

                    if (UIManager.Instance != null)
                        UIManager.Instance.ShowHitMarker(target.IsDead);
                }
            }
        }

        if (impactEffect != null)
        {
            GameObject fx = Instantiate(
                impactEffect, point,
                Quaternion.LookRotation(-transform.forward));
            Destroy(fx, 2f);
        }

        Destroy(gameObject);
    }
}
