using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// Only lets the chair report impacts while a player is riding it. Bumping an empty chair
    /// around on foot still moves it, but no longer throws sparks, sound and camera shake at the
    /// player standing next to it.
    /// </summary>
    [DisallowMultipleComponent]
    public class RiderImpactGate : MonoBehaviour, IImpactGate
    {
        [Tooltip("Seat that decides whether the chair is being ridden. Resolved on this object.")]
        [SerializeField] private ChairSeat seat;

        public bool ImpactAllowed => seat != null && seat.IsOccupied;

        private void Awake()
        {
            if (seat == null)
                seat = GetComponent<ChairSeat>();
        }
    }
}
