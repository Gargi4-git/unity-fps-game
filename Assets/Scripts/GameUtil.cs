using UnityEngine;

/// <summary>
/// Small shared helpers.
/// </summary>
public static class GameUtil
{
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    // Works on both Unity 6 (linearVelocity) and older versions (velocity).
    public static Vector3 GetVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    public static void SetVelocity(Rigidbody rb, Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }

    /// <summary>
    /// Raycast that returns the closest hit which is NOT part of ignoreRoot,
    /// not a trigger, and not a bullet. Fixes rays being blocked by the
    /// shooter's own colliders (gun, body, ...).
    /// </summary>
    public static bool RaycastIgnoring(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        Transform ignoreRoot,
        out RaycastHit result,
        int layerMask = Physics.DefaultRaycastLayers)
    {
        int count = Physics.RaycastNonAlloc(
            origin, direction, hitBuffer, maxDistance,
            layerMask, QueryTriggerInteraction.Ignore);

        result = default(RaycastHit);
        float best = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];

            if (ignoreRoot != null && h.transform.IsChildOf(ignoreRoot))
                continue;

            if (h.collider.GetComponentInParent<Bullet>() != null)
                continue;

            if (h.distance < best)
            {
                best = h.distance;
                result = h;
                found = true;
            }
        }

        return found;
    }
}
