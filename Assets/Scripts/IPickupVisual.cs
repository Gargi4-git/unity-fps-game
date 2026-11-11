using UnityEngine;

/// <summary>
/// Decorator pattern - the common interface for how a pickup looks.
/// </summary>
public interface IPickupVisual
{
    void Apply(Material material);
    void Restore();
}
