using System.Collections;
using UnityEngine;

public class Gun : MonoBehaviour
{
    public string weaponName = "Rifle";

    [Header("Stats")]
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magSize = 20;
    [Tooltip("Random bullet spread in degrees (0 = perfectly accurate).")]
    public float spread = 0f;
    public float maxAimDistance = 150f;

    [Header("Recoil")]
    public float recoilDistance = 0.1f;
    public float recoilSpeed = 15f;

    [Header("Audio")]
    public AudioClip shootingSFX;
    public AudioClip reloadSFX;
    public AudioClip emptySFX;

    [Header("References")]
    public GameObject bullet;
    public Transform bulletSpawnPoint;
    public GameObject weaponFlash;
    public GameObject droppedWeapon;

    // Set automatically by PlayerShooting. Falls back to Camera.main.
    [HideInInspector] public Camera aimCamera;

    private int currentAmmo;
    private bool isReloading = false;
    private float reloadProgress = 0f;
    private float nextTimeToFire = 0f;

    private Quaternion initialRotation;
    private Vector3 initialPosition;
    private Vector3 reloadRotationOffset = new Vector3(66, 50, 50);
    private Coroutine recoilRoutine;

    public int CurrentAmmo { get { return currentAmmo; } }
    public int MagSize { get { return magSize; } }
    public bool IsReloading { get { return isReloading; } }
    public float ReloadProgress { get { return reloadProgress; } }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(weaponName)) return weaponName;
            return gameObject.name.Replace("(Clone)", "").Trim();
        }
    }

    // Ammo is set in Awake so PickUp can restore saved ammo right after
    // spawning the weapon (before Start would overwrite it).
    void Awake()
    {
        currentAmmo = magSize;
    }

    void Start()
    {
        initialRotation = transform.localRotation;
        initialPosition = transform.localPosition;
    }

    void OnDisable()
    {
        // Don't get stuck "reloading" if the weapon is disabled mid-reload.
        if (isReloading)
        {
            StopAllCoroutines();
            isReloading = false;
            reloadProgress = 0f;
            transform.localRotation = initialRotation;
            transform.localPosition = initialPosition;
        }
    }

    public void SetAmmo(int ammo)
    {
        currentAmmo = Mathf.Clamp(ammo, 0, magSize);
    }

    public void Shoot()
    {
        if (isReloading) return;
        if (Time.time < nextTimeToFire) return;

        if (currentAmmo <= 0)
        {
            nextTimeToFire = Time.time + 0.3f;
            if (emptySFX != null) AudioManager.Instance.PlaySFX(emptySFX, 0.5f);
            TryReload();
            return;
        }

        if (bullet == null)
        {
            Debug.LogError("Bullet prefab is not assigned!", this);
            return;
        }

        if (bulletSpawnPoint == null)
        {
            Debug.LogError("Bullet Spawn Point is not assigned!", this);
            return;
        }

        nextTimeToFire = Time.time + fireRate;
        currentAmmo--;
        GameManager.NotifyShotFired();

        Vector3 origin = bulletSpawnPoint.position;
        Vector3 direction = GetAimDirection(origin);

        if (spread > 0f)
        {
            direction = Quaternion.Euler(
                Random.Range(-spread, spread),
                Random.Range(-spread, spread), 0f) * direction;
        }

        AudioManager.Instance.PlaySFX(shootingSFX, 0.25f);

        GameObject spawned = Instantiate(
            bullet, origin, Quaternion.LookRotation(direction));

        Bullet b = spawned.GetComponent<Bullet>();

        if (b != null)
        {
            PlayerHealth owner = GetComponentInParent<PlayerHealth>();
            b.SetOwner(owner != null ? owner.gameObject : transform.root.gameObject);
        }

        if (weaponFlash != null)
        {
            GameObject flash = Instantiate(
                weaponFlash, origin, bulletSpawnPoint.rotation, bulletSpawnPoint);
            flash.SetActive(true);
            Destroy(flash, 0.15f);
        }

        if (recoilRoutine != null)
        {
            StopCoroutine(recoilRoutine);
            transform.localPosition = initialPosition;
        }

        recoilRoutine = StartCoroutine(Recoil());

        // Auto-reload when the mag runs dry.
        if (currentAmmo == 0)
            TryReload();
    }

    // Bullets fly from the muzzle towards whatever the crosshair points at,
    // so shots always land where the player aims (replaces the old
    // hard-coded -3/-2 degree offset).
    private Vector3 GetAimDirection(Vector3 origin)
    {
        Camera cam = aimCamera != null ? aimCamera : Camera.main;

        if (cam == null)
            return bulletSpawnPoint.forward;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 target;
        RaycastHit hit;

        if (GameUtil.RaycastIgnoring(
            ray.origin, ray.direction, maxAimDistance, transform.root, out hit))
        {
            target = hit.point;
        }
        else
        {
            target = ray.GetPoint(maxAimDistance);
        }

        Vector3 dir = target - origin;

        // Target is too close / behind the muzzle: just shoot straight ahead.
        if (dir.sqrMagnitude < 0.01f || Vector3.Dot(dir, ray.direction) < 0f)
            return ray.direction;

        return dir.normalized;
    }

    IEnumerator Reload()
    {
        isReloading = true;
        reloadProgress = 0f;

        AudioManager.Instance.PlaySFX(reloadSFX, 0.6f);

        if (recoilRoutine != null)
        {
            StopCoroutine(recoilRoutine);
            recoilRoutine = null;
            transform.localPosition = initialPosition;
        }

        Quaternion targetRotation = Quaternion.Euler(
            initialRotation.eulerAngles + reloadRotationOffset);

        float total = Mathf.Max(0.1f, reloadTime);
        float half = total / 2f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            reloadProgress = Mathf.Clamp01(t / total);
            transform.localRotation = Quaternion.Lerp(
                initialRotation, targetRotation, t / half);
            yield return null;
        }

        t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            reloadProgress = Mathf.Clamp01(0.5f + t / total);
            transform.localRotation = Quaternion.Lerp(
                targetRotation, initialRotation, t / half);
            yield return null;
        }

        transform.localRotation = initialRotation;
        currentAmmo = magSize;
        reloadProgress = 1f;
        isReloading = false;
    }

    public void TryReload()
    {
        if (isReloading) return;
        if (currentAmmo == magSize) return;
        if (!isActiveAndEnabled) return;

        StartCoroutine(Reload());
    }

    // Kick the gun back along its barrel direction and return.
    private IEnumerator Recoil()
    {
        Vector3 backLocal;

        Vector3 backWorld = bulletSpawnPoint != null
            ? -bulletSpawnPoint.forward
            : -transform.forward;

        backLocal = transform.parent != null
            ? transform.parent.InverseTransformDirection(backWorld)
            : backWorld;

        Vector3 recoilTarget = initialPosition + backLocal * recoilDistance;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * recoilSpeed;
            transform.localPosition = Vector3.Lerp(initialPosition, recoilTarget, t);
            yield return null;
        }

        t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * recoilSpeed;
            transform.localPosition = Vector3.Lerp(recoilTarget, initialPosition, t);
            yield return null;
        }

        transform.localPosition = initialPosition;
        recoilRoutine = null;
    }

    /// <summary>
    /// Spawns the pickup version of this weapon in front of the player
    /// (remembering the ammo). Returns true on success. The caller
    /// (PlayerShooting) removes the equipped weapon afterwards.
    /// </summary>
    public bool Drop()
    {
        if (droppedWeapon == null)
        {
            Debug.LogError("Dropped Weapon Prefab is not assigned!", this);
            return false;
        }

        Camera cam = aimCamera != null ? aimCamera : Camera.main;

        Vector3 position = transform.position;
        Vector3 throwDirection = transform.forward;

        if (cam != null)
        {
            Vector3 fwd = cam.transform.forward;
            position = cam.transform.position + fwd * 1f + Vector3.down * 0.3f;
            throwDirection = fwd;

            // Don't spawn the weapon inside a wall.
            RaycastHit hit;
            if (GameUtil.RaycastIgnoring(
                cam.transform.position, fwd, 1f, transform.root, out hit))
            {
                position = hit.point - fwd * 0.25f;
            }
        }

        GameObject dropped = Instantiate(droppedWeapon, position, transform.rotation);

        PickUp pickUp = dropped.GetComponentInChildren<PickUp>(true);

        if (pickUp != null)
            pickUp.storedAmmo = currentAmmo;
        else
            Debug.LogWarning("Dropped weapon prefab needs a PickUp component!", dropped);

        Rigidbody droppedBody = dropped.GetComponentInChildren<Rigidbody>();

        if (droppedBody != null)
            GameUtil.SetVelocity(droppedBody, throwDirection * 4f + Vector3.up * 1.5f);

        return true;
    }
}
