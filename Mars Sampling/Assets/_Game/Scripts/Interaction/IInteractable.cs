namespace MarsSampling
{
    /// <summary>
    /// Marker interface for anything the player can tap.
    /// PlayerInteractor raycasts taps, finds an IInteractable and hands it to
    /// MissionManager, which decides what the tap means in the current phase.
    /// </summary>
    public interface IInteractable
    {
    }
}
