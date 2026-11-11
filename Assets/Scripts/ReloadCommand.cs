public sealed class ReloadCommand : ICommand
{
    private readonly Gun gun;

    public ReloadCommand(Gun gun)
    {
        this.gun = gun;
    }

    public void Execute()
    {
        gun.TryReload();
    }
}
