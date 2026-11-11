public sealed class DropCommand : ICommand
{
    private readonly PlayerShooting player;

    public DropCommand(PlayerShooting player)
    {
        this.player = player;
    }

    public void Execute()
    {
        player.DropCurrentWeapon(false);
    }
}
