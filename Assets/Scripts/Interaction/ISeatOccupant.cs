namespace FireExtinguisher.Interaction
{
    /// <summary>
    /// The player-side half of sitting. Seats call into an occupant only through this interface,
    /// never through a concrete player class.
    /// </summary>
    public interface ISeatOccupant
    {
        bool IsSeated { get; }

        /// <summary>The seat currently occupied, or null.</summary>
        ISeat CurrentSeat { get; }

        /// <summary>Attempts to occupy a seat. False when already seated or the seat is taken.</summary>
        bool TrySit(ISeat seat);

        /// <summary>Leaves the current seat if there is room. No-op when not seated.</summary>
        void StandUp();

        /// <summary>Called when the occupied seat was disabled or destroyed. Leaves without a room check.</summary>
        void OnSeatLost(ISeat seat);
    }
}
