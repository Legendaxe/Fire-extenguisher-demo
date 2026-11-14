using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Seated mode. While the player is seated this provider shadows aiming and turns the
    /// Interact press into standing up. It needs only <see cref="ISeatOccupant"/> semantics.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeatController))]
    public class SeatInteractionProvider : MonoBehaviour, IInteractionProvider
    {
        [Header("References")]
        [SerializeField] private SeatController occupant;

        [Header("Ordering")]
        [Tooltip("Higher than normal aiming so the chair cannot be re-targeted while seated.")]
        [SerializeField] private int priority = 100;

        private StandInteraction _standInteraction;

        public int Priority => priority;
        public bool IsAvailable => occupant != null && occupant.IsSeated;

        private void Awake()
        {
            if (occupant == null)
                occupant = GetComponent<SeatController>();

            if (occupant != null)
                _standInteraction = new StandInteraction(occupant);
        }

        public IInteractable Resolve(GameObject interactor)
        {
            return IsAvailable ? _standInteraction : null;
        }

        public bool TryInteract(GameObject interactor)
        {
            if (occupant == null || !occupant.IsSeated)
                return false;

            occupant.StandUp();
            return true;
        }
    }
}
