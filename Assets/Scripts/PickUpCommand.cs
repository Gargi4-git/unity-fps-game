public sealed class PickUpCommand : ICommand
{
    private readonly PickUp pickup;
    private readonly PlayerShooting player;

    public PickUpCommand(PickUp pickup, PlayerShooting player)
    {
        this.pickup = pickup;
        this.player = player;
    }

    public void Execute()
    {
        pickup.OnPickUp(player);
    }
}
