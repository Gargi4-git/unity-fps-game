using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable, ISubject
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

    // Observer pattern: everything that wants to know when the player is hurt.
    private readonly List<IObserver> observers = new List<IObserver>();

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

        // Camera shake, hit overlay and hit sound are done by the observers.
        Notify();

        if (health <= 0)
            Die();
    }

    // ------------------------------------------------ ISubject (Observer)

    public void Attach(IObserver observer)
    {
        if (observer != null && !observers.Contains(observer))
            observers.Add(observer);
    }

    public void Detach(IObserver observer)
    {
        observers.Remove(observer);
    }

    public void Notify()
    {
        foreach (IObserver observer in observers.ToArray())
            observer.OnSubjectChanged(this);
    }

    private void OnDestroy()
    {
        observers.Clear();
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
