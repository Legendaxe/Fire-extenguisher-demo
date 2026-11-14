using UnityEngine;

namespace FireExtinguisher.Interaction
{
    /// <summary>
    /// Marks a chair as sittable and owns its seat anchor and occupancy.
    /// Lives on the chair root, so the anchor follows the chair's Rigidbody.
    /// Talks to whoever sits down only through <see cref="ISeatOccupant"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChairSeat : MonoBehaviour, ISeat
    {
        [SerializeField] private Transform sitPoint;
        [SerializeField] private ChairSittingConfig config;
        [SerializeField] private string fallbackSitPrompt = "Sit";
        [SerializeField] private string fallbackStandPrompt = "Stand";

        private ISeatOccupant _occupant;

        public Transform SitPoint => sitPoint != null ? sitPoint : transform;
        public Transform SeatTransform => transform;
        public ChairSittingConfig Config => config;
        public bool IsOccupied => _occupant != null;
        public ISeatOccupant Occupant => _occupant;

        public string SitPrompt => config != null ? config.SitPrompt : fallbackSitPrompt;
        public string StandPrompt => config != null ? config.StandPrompt : fallbackStandPrompt;
        public string InteractionPrompt => SitPrompt;

        private void Awake()
        {
            if (sitPoint == null)
                sitPoint = transform.Find("SitPoint");
        }

        public bool CanInteract(GameObject interactor)
        {
            if (IsOccupied || interactor == null)
                return false;

            return interactor.TryGetComponent(out ISeatOccupant occupant) && !occupant.IsSeated;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor != null && interactor.TryGetComponent(out ISeatOccupant occupant))
                occupant.TrySit(this);
        }

        public bool TryReserve(ISeatOccupant occupant)
        {
            if (occupant == null || IsOccupied)
                return false;

            _occupant = occupant;
            return true;
        }

        public void Release(ISeatOccupant occupant)
        {
            if (ReferenceEquals(_occupant, occupant))
                _occupant = null;
        }

        private void OnDisable()
        {
            // Clear first so the occupant's release is a no-op and cannot re-enter this seat.
            ISeatOccupant occupant = _occupant;
            _occupant = null;

            // A disabled or destroyed chair must never strand its occupant.
            if (occupant is MonoBehaviour behaviour && behaviour != null)
                occupant.OnSeatLost(this);
        }
    }
}
