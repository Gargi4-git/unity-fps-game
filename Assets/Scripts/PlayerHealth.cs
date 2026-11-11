using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public AudioClip hitSFX;
    public AudioClip deathSFX;

    public int maxHealth = 100;
    public int health = 100;

    [Header("Hit feedback")]
    public float shakeDuration = 0.3f;
    public float shakeMagnitude = 0.08f;

    [Header("Misc")]
    public float killHeight = -50f;   // falling out of the world kills you
    public bool godMode = false;

    private bool isDead = false;

    public bool IsDead { get { return isDead; } }

    private void Start()
    {
        maxHealth = Mathf.Max(maxHealth, health, 1);
        health = Mathf.Clamp(health, 1, maxHealth);
    }

    private void Update()
    {
        if (!isDead && transform.position.y < killHeight)
        {
            health = 0;
            Die();
        }
    }

    public void TakeDamage(int amount)
    {
        if (isDead || godMode || amount <= 0)
            return;

        health = Mathf.Max(0, health - amount);

        if (PlayerLook.Instance != null)
            PlayerLook.Instance.AddShake(shakeDuration, shakeMagnitude);

        if (UIManager.Instance != null)
            UIManager.Instance.InstantiateHitUI();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(hitSFX);

        if (health <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (isDead || amount <= 0)
            return;

        health = Mathf.Min(maxHealth, health + amount);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        health = 0;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(deathSFX);

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
        else
            Time.timeScale = 0f;
    }
}
