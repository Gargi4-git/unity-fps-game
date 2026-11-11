using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;   // hold Left Shift
    public float jumpForce = 5f;

    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    public AudioClip footStep;

    private Rigidbody rb;
    private Collider bodyCollider;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool isSprinting;
    private readonly Collider[] groundHits = new Collider[8];

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody is missing from the Player!", this);
            enabled = false;
            return;
        }

        rb.freezeRotation = true;

        bodyCollider = GetComponent<Collider>();

        // Zero-friction material so the player doesn't stick to walls
        // while jumping against them.
        if (bodyCollider != null && bodyCollider.sharedMaterial == null)
        {
#if UNITY_6000_0_OR_NEWER
            PhysicsMaterial mat = new PhysicsMaterial("PlayerNoFriction");
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
#else
            PhysicMaterial mat = new PhysicMaterial("PlayerNoFriction");
            mat.frictionCombine = PhysicMaterialCombine.Minimum;
#endif
            mat.dynamicFriction = 0f;
            mat.staticFriction = 0f;
            bodyCollider.sharedMaterial = mat;
        }

        StartCoroutine(PlayFootStep());
    }

    void Update()
    {
        CheckGround();

        isSprinting = Keyboard.current != null &&
                      Keyboard.current.leftShiftKey.isPressed &&
                      moveInput.y > 0.1f;
    }

    void FixedUpdate()
    {
        MovePlayer();
    }

    public void OnJump(InputValue value)
    {
        if (!value.isPressed || !GameManager.IsPlaying)
            return;

        if (!isGrounded || rb == null)
            return;

        // Reset vertical speed so every jump is the same height.
        Vector3 v = GameUtil.GetVelocity(rb);
        v.y = 0f;
        GameUtil.SetVelocity(rb, v);

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    public void OnMovement(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void CheckGround()
    {
        Vector3 pos;
        float radius = groundDistance;

        if (groundCheck != null)
        {
            pos = groundCheck.position;
        }
        else if (bodyCollider != null)
        {
            Bounds b = bodyCollider.bounds;
            pos = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
        }
        else
        {
            isGrounded = false;
            return;
        }

        // If no ground mask was set, treat everything as ground.
        int mask = groundMask.value == 0
            ? Physics.DefaultRaycastLayers
            : groundMask.value;

        int count = Physics.OverlapSphereNonAlloc(
            pos, radius, groundHits, mask, QueryTriggerInteraction.Ignore);

        isGrounded = false;

        for (int i = 0; i < count; i++)
        {
            // Ignore the player's own colliders.
            if (!groundHits[i].transform.IsChildOf(transform))
            {
                isGrounded = true;
                break;
            }
        }
    }

    void MovePlayer()
    {
        if (rb == null)
            return;

        Vector3 direction =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        if (!GameManager.IsPlaying)
            direction = Vector3.zero;

        float speed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);
        Vector3 current = GameUtil.GetVelocity(rb);

        GameUtil.SetVelocity(rb, new Vector3(
            direction.x * speed,
            current.y,
            direction.z * speed));
    }

    IEnumerator PlayFootStep()
    {
        WaitForSeconds walk = new WaitForSeconds(0.45f);
        WaitForSeconds run = new WaitForSeconds(0.3f);

        while (true)
        {
            Vector3 v = GameUtil.GetVelocity(rb);
            v.y = 0f;

            if (v.sqrMagnitude > 0.01f && isGrounded &&
                footStep != null && GameManager.IsPlaying)
            {
                AudioManager.Instance.PlaySFX(footStep, 0.6f);
            }

            yield return isSprinting ? run : walk;
        }
    }
}
