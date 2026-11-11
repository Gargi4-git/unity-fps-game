public sealed class ShootCommand : ICommand
{
    private readonly Gun gun;

    public ShootCommand(Gun gun)
    {
        this.gun = gun;
    }

    public void Execute()
    {
        gun.Shoot();
    }
}
