using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    public static PlayerLook Instance;

    [Tooltip("Degrees per mouse pixel. Can be changed in the pause menu.")]
    public float lookSensitivity = 0.15f;
    public float maxLookAngle = 85f;
    public bool invertY = false;
    public Transform cam;

    private float xRotation = 0f;
    private Vector2 lookInput;

    private float shakeTime = 0f;
    private float shakeMagnitude = 0.1f;
    private float shakeFadeSpeed = 1.5f;
    private Vector3 initialCamPos;
    private float deathRoll = 0f;

    private void Awake()
    {
        Instance = this;

        if (cam == null)
        {
            Camera c = GetComponentInChildren<Camera>();
            if (c != null) cam = c.transform;
        }
    }

    void Start()
    {
        lookSensitivity = PlayerPrefs.GetFloat("LookSensitivity", lookSensitivity);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cam != null)
            initialCamPos = cam.localPosition;
    }

    void Update()
    {
        if (cam == null)
            return;

        HandleMouseLook();
        HandleShake();
        HandleDeathCam();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    void HandleMouseLook()
    {
        if (!GameManager.IsPlaying)
            return;

        // Mouse delta is already "per frame", so no Time.deltaTime here.
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity * (invertY ? -1f : 1f);

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        cam.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleShake()
    {
        if (shakeTime > 0f)
        {
            float strength = Mathf.Clamp01(shakeTime);
            cam.localPosition = initialCamPos +
                Random.insideUnitSphere * shakeMagnitude * strength;
            shakeTime -= Time.unscaledDeltaTime * shakeFadeSpeed;
        }
        else
        {
            cam.localPosition = initialCamPos;
        }
    }

    // Camera slowly tilts over when the player dies.
    void HandleDeathCam()
    {
        GameState s = GameManager.CurrentState;
        bool dead = s == GameState.Dying || s == GameState.GameOver;

        float target = dead ? 65f : 0f;
        deathRoll = Mathf.MoveTowards(deathRoll, target, 90f * Time.unscaledDeltaTime);

        if (dead || deathRoll > 0.01f)
            cam.localRotation = Quaternion.Euler(xRotation, 0f, deathRoll);
    }

    public void AddShake(float duration, float magnitude)
    {
        shakeTime = duration;
        shakeMagnitude = magnitude;
    }
}
