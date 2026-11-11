using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShooting : MonoBehaviour
{
    public Gun gun;
    public Transform gunHolder;
    public Camera playerCamera;

    [Header("Pickup")]
    public float pickupRange = 5f;
    [Range(0.5f, 1f)] public float pickupMinDot = 0.8f;

    private bool isHoldingShoot = false;
    private PickUp currentPickup;

    // Command pattern: each player action is a command object.
    private ICommand shootCommand;
    private ICommand reloadCommand;
    private ICommand dropCommand;

    public PickUp CurrentPickup { get { return currentPickup; } }

    // -------------------------------------------------------------- commands

    private void Awake()
    {
        ConfigureCommands();
    }

    private void ConfigureCommands()
    {
        dropCommand = new DropCommand(this);

        if (gun == null)
        {
            shootCommand = null;
            reloadCommand = null;
            return;
        }

        shootCommand = new ShootCommand(gun);
        reloadCommand = new ReloadCommand(gun);
    }

    // Always use this when the equipped weapon changes, so the commands follow it.
    public void SetGun(Gun newGun)
    {
        gun = newGun;
        ConfigureCommands();
    }

    // ---------------------------------------------------------------- input

    public void OnShoot(InputValue value)
    {
        isHoldingShoot = value.isPressed && GameManager.IsPlaying;
    }

    public void OnReload(InputValue value)
    {
        if (!value.isPressed || !GameManager.IsPlaying)
            return;

        if (reloadCommand != null)
            reloadCommand.Execute();
    }

    public void OnPickUp(InputValue value)
    {
        if (!value.isPressed || !GameManager.IsPlaying)
            return;

        if (currentPickup != null)
        {
            new PickUpCommand(currentPickup, this).Execute();
            currentPickup = null;
        }
    }

    public void OnDrop(InputValue value)
    {
        if (!value.isPressed || !GameManager.IsPlaying)
            return;

        dropCommand.Execute();
    }

    // ----------------------------------------------------------------- life

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (gunHolder == null)
        {
            Debug.LogError("Gun Holder is not assigned!", this);
            return;
        }

        // Find the weapon that is already equipped under the Gun Holder.
        Gun equipped = gunHolder.GetComponentInChildren<Gun>(true);

        if (equipped != null)
        {
            SetGun(equipped);
            gun.aimCamera = playerCamera;
        }
        else
        {
            SetGun(null);
        }
    }

    private void Update()
    {
        if (!GameManager.IsPlaying)
        {
            isHoldingShoot = false;
            SetCurrentPickup(null);
            return;
        }

        UpdatePickupTarget();

        if (isHoldingShoot && shootCommand != null)
            shootCommand.Execute();
    }

    // -------------------------------------------------------------- pickups

    private void UpdatePickupTarget()
    {
        SetCurrentPickup(FindBestPickup());
    }

    private void SetCurrentPickup(PickUp pickup)
    {
        if (pickup == currentPickup)
            return;

        if (currentPickup != null)
            currentPickup.SetHighlighted(false);

        currentPickup = pickup;

        if (currentPickup != null)
            currentPickup.SetHighlighted(true);
    }

    private PickUp FindBestPickup()
    {
        if (playerCamera == null)
            return null;

        Transform cam = playerCamera.transform;
        PickUp best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < PickUp.All.Count; i++)
        {
            PickUp p = PickUp.All[i];

            if (p == null || !p.isActiveAndEnabled)
                continue;

            Vector3 to = p.transform.position - cam.position;
            float dist = to.magnitude;

            if (dist > pickupRange || dist < 0.01f)
                continue;

            Vector3 dir = to / dist;

            if (Vector3.Dot(cam.forward, dir) < pickupMinDot)
                continue;

            // Can't pick things up through walls.
            RaycastHit hit;
            if (GameUtil.RaycastIgnoring(cam.position, dir, dist, transform.root, out hit))
            {
                bool isThePickup =
                    hit.transform.IsChildOf(p.transform) ||
                    p.transform.IsChildOf(hit.transform);

                if (!isThePickup && hit.distance < dist - 0.4f)
                    continue;
            }

            float score = Vector3.Dot(cam.forward, dir) - dist * 0.02f;

            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        return best;
    }

    // ------------------------------------------------------------ equipment

    /// <summary>
    /// Equips a weapon prefab. Any weapon already held is dropped first.
    /// ammo &lt; 0 means a full magazine.
    /// </summary>
    public bool Equip(GameObject weaponPrefab, int ammo)
    {
        if (weaponPrefab == null || gunHolder == null)
        {
            Debug.LogError("Cannot equip: weapon prefab or Gun Holder missing.", this);
            return false;
        }

        // Make sure the prefab is valid before we throw away the old weapon.
        if (weaponPrefab.GetComponentInChildren<Gun>(true) == null)
        {
            Debug.LogError("Equipped weapon prefab has no Gun component!", weaponPrefab);
            return false;
        }

        DropCurrentWeapon(true);

        GameObject newWeapon = Instantiate(weaponPrefab, gunHolder);
        newWeapon.transform.localPosition = Vector3.zero;
        newWeapon.transform.localRotation = Quaternion.identity;

        Gun newGun = newWeapon.GetComponentInChildren<Gun>(true);

        if (newGun == null)
        {
            Destroy(newWeapon);
            return false;
        }

        newGun.aimCamera = playerCamera;

        if (ammo >= 0)
            newGun.SetAmmo(ammo);

        SetGun(newGun);
        return true;
    }

    public void DropCurrentWeapon(bool destroyEvenIfCantDrop)
    {
        if (gun == null)
            return;

        bool dropped = gun.Drop();

        if (!dropped && !destroyEvenIfCantDrop)
            return;

        Transform root = GetWeaponRoot(gun);
        SetGun(null);
        isHoldingShoot = false;

        if (root != null)
            Destroy(root.gameObject);
    }

    // The weapon may be nested (Gun component on a child of the model root).
    private Transform GetWeaponRoot(Gun g)
    {
        Transform t = g.transform;

        while (t.parent != null && t.parent != gunHolder)
            t = t.parent;

        return t.parent == gunHolder ? t : g.transform;
    }
}
