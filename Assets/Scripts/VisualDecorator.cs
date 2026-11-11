using UnityEngine;

/// <summary>
/// Decorator pattern - base decorator. Wraps another IPickupVisual and
/// forwards everything to it unless a subclass overrides it.
/// </summary>
public abstract class VisualDecorator : IPickupVisual
{
    protected readonly IPickupVisual wrappedVisual;

    protected VisualDecorator(IPickupVisual wrappedVisual)
    {
        this.wrappedVisual = wrappedVisual;
    }

    public virtual void Apply(Material material)
    {
        wrappedVisual.Apply(material);
    }

    public virtual void Restore()
    {
        wrappedVisual.Restore();
    }
}
