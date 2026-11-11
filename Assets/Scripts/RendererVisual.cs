using UnityEngine;

/// <summary>
/// Decorator pattern - the basic object that really changes the renderers.
/// Remembers the original materials so they can be restored.
/// </summary>
public sealed class RendererVisual : IPickupVisual
{
    private readonly Renderer[] renderers;
    private readonly Material[][] originalMaterials;

    public RendererVisual(Renderer[] renderers)
    {
        this.renderers = renderers;
        originalMaterials = new Material[renderers.Length][];

        // sharedMaterials (not .materials) so we don't leak material copies.
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].sharedMaterials;
    }

    public void Apply(Material material)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            Material[] mats = new Material[originalMaterials[i].Length];

            for (int j = 0; j < mats.Length; j++)
                mats[j] = material;

            renderers[i].sharedMaterials = mats;
        }
    }

    public void Restore()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            renderers[i].sharedMaterials = originalMaterials[i];
        }
    }
}
