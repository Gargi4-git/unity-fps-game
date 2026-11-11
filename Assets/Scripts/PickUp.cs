using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A weapon lying on the ground. PlayerShooting decides which pickup the
/// player is looking at and highlights/collects it.
/// </summary>
public class PickUp : MonoBehaviour
{
    public static readonly List<PickUp> All = new List<PickUp>();

    public Material highlightedMaterial;
    public GameObject weaponPrefab;
    public string displayName;
    public AudioClip pickupSFX;

    // Ammo left in the weapon when it was dropped (-1 = full / unknown).
    [HideInInspector] public int storedAmmo = -1;

    private Renderer[] renderers;
    private Material[][] originalMaterials;
    private bool isHighlighted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            if (weaponPrefab != null)
            {
                Gun g = weaponPrefab.GetComponentInChildren<Gun>(true);
                if (g != null) return g.DisplayName;
            }

            return gameObject.name.Replace("(Clone)", "").Trim();
        }
    }

    private void Awake()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        List<Renderer> valid = new List<Renderer>();

        foreach (Renderer r in all)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;

            valid.Add(r);
        }

        renderers = valid.ToArray();
        originalMaterials = new Material[renderers.Length][];

        // sharedMaterials (not .materials) so we don't leak material copies.
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].sharedMaterials;
    }

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public void SetHighlighted(bool enable)
    {
        if (enable == isHighlighted || highlightedMaterial == null)
            return;

        isHighlighted = enable;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            if (enable)
            {
                Material[] mats = new Material[originalMaterials[i].Length];

                for (int j = 0; j < mats.Length; j++)
                    mats[j] = highlightedMaterial;

                renderers[i].sharedMaterials = mats;
            }
            else
            {
                renderers[i].sharedMaterials = originalMaterials[i];
            }
        }
    }

    // Kept for backwards compatibility.
    public void OnPickUp()
    {
        PlayerShooting player = FindFirstObjectByType<PlayerShooting>();

        if (player == null)
        {
            Debug.LogError("PlayerShooting not found!", this);
            return;
        }

        OnPickUp(player);
    }

    public void OnPickUp(PlayerShooting player)
    {
        if (player == null)
            return;

        if (weaponPrefab == null)
        {
            Debug.LogError("Weapon Prefab is not assigned!", this);
            return;
        }

        // If the player already holds a weapon it is dropped (swapped)
        // instead of being deleted.
        if (!player.Equip(weaponPrefab, storedAmmo))
            return;

        if (pickupSFX != null)
            AudioManager.Instance.PlaySFX(pickupSFX, 0.7f);

        All.Remove(this);
        Destroy(gameObject);
    }
}
