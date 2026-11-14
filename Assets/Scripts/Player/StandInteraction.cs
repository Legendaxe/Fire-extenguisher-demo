using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Pseudo-interactable that represents "stand up" while the player is seated, so the
    /// interactor and the prompt UI can treat every interaction uniformly.
    /// </summary>
    internal sealed class StandInteraction : IInteractable
    {
        private readonly ISeatOccupant _occupant;

        public StandInteraction(ISeatOccupant occupant)
        {
            _occupant = occupant;
        }

        public string InteractionPrompt => _occupant.CurrentSeat?.StandPrompt ?? string.Empty;

        public bool CanInteract(GameObject interactor) => _occupant.IsSeated;

        public void Interact(GameObject interactor) => _occupant.StandUp();
    }
}
