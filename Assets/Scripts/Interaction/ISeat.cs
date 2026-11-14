using UnityEngine;

namespace FireExtinguisher.Interaction
{
    /// <summary>
    /// A place a player can occupy, such as a chair. The seat owns its anchor and occupancy and
    /// talks to its occupant only through <see cref="ISeatOccupant"/>, so it has no dependency on
    /// any player type.
    /// </summary>
    public interface ISeat : IInteractable
    {
        /// <summary>Where the occupant is placed. Must be non-null.</summary>
        Transform SitPoint { get; }

        /// <summary>Root transform of the seat, used for stand-up probing.</summary>
        Transform SeatTransform { get; }

        /// <summary>Tuning shared by every seat of this kind. May be null.</summary>
        ChairSittingConfig Config { get; }

        /// <summary>Verb shown while seated, for example "Stand".</summary>
        string StandPrompt { get; }

        /// <summary>Claims this seat for one occupant. False when it is already taken.</summary>
        bool TryReserve(ISeatOccupant occupant);

        /// <summary>Frees the seat. Only the current occupant can release it.</summary>
        void Release(ISeatOccupant occupant);
    }
}
