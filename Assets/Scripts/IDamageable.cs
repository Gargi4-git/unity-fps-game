/// <summary>
/// Anything that can be hurt by a Bullet (Player, Enemy...).
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int amount);
}
