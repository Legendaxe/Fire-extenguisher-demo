using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Owns the seated state of one player. While seated the body is kinematic, the capsule is
    /// off, movement is disabled and the player follows the seat anchor in position and
    /// rotation, so the view rides the chair as it tips. The seat's own Rigidbody is never
    /// touched, so it stays dynamic and can still be pushed.
    /// Implements <see cref="ISeatOccupant"/> so seats never need to know this type.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class SeatController : MonoBehaviour, ISeatOccupant
    {
        [Header("References")]
        [SerializeField] private Rigidbody body;
        [SerializeField] private CapsuleCollider bodyCollider;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;

        [Header("Defaults")]
        [Tooltip("Used when a seat has no ChairSittingConfig assigned.")]
        [SerializeField] private ChairSittingConfig defaultConfig;

        private ISeat _seat;
        private Vector3 _preSeatPosition;
        private RigidbodyInterpolation _preSeatInterpolation;
        private float _resitBlockedUntil;

        public bool IsSeated => _seat != null;
        public ISeat CurrentSeat => _seat;

        /// <summary>Raised with the seat and the new seated state.</summary>
        public event System.Action<ISeat, bool> SeatedChanged;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (bodyCollider == null) bodyCollider = GetComponent<CapsuleCollider>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (look == null) look = GetComponent<PlayerLook>();
        }

        private void LateUpdate()
        {
            if (_seat == null)
                return;

            // The seat moves through physics; ride its anchor, including the chair's tilt, so
            // the camera is attached to the chair instead of the world.
            Transform anchor = _seat.SitPoint;
            Quaternion rotation = look != null ? look.GetSeatedRotation() : anchor.rotation;
            transform.SetPositionAndRotation(anchor.position, rotation);
        }

        public bool TrySit(ISeat seat)
        {
            if (seat == null || IsSeated || Time.time < _resitBlockedUntil)
                return false;

            if (!seat.TryReserve(this))
                return false;

            ChairSittingConfig config = ResolveConfig(seat);
            Transform anchor = seat.SitPoint;

            _seat = seat;
            _preSeatPosition = transform.position;
            _preSeatInterpolation = body != null ? body.interpolation : RigidbodyInterpolation.Interpolate;

            if (motor != null)
                motor.enabled = false;

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.interpolation = RigidbodyInterpolation.None;
                body.isKinematic = true;
            }

            if (bodyCollider != null)
                bodyCollider.enabled = false;

            if (look != null)
            {
                look.SetSeatedView(true, config.SeatedEyeHeight);
                look.SetSeatFrame(anchor, config.SeatedYawLimit);
            }

            transform.SetPositionAndRotation(
                anchor.position,
                look != null ? look.GetSeatedRotation() : anchor.rotation);

            Physics.SyncTransforms();
            SeatedChanged?.Invoke(seat, true);
            return true;
        }

        /// <summary>Stands up if there is room next to the seat.</summary>
        public void StandUp()
        {
            if (_seat == null)
                return;

            ISeat seat = _seat;
            ChairSittingConfig config = ResolveConfig(seat);

            if (!TryFindStandPosition(seat, config, out Vector3 standPosition))
            {
                Debug.LogWarning($"No room to stand next to '{Describe(seat)}'. Staying seated.", this);
                return;
            }

            ExitSeat(seat, standPosition, config);
        }

        /// <summary>Leaves the seat without a room check. Used when the seat is disabled or destroyed.</summary>
        public void ForceStand()
        {
            if (_seat == null)
                return;

            ISeat seat = _seat;
            ChairSittingConfig config = ResolveConfig(seat);

            Vector3 standPosition = _preSeatPosition;
            if (TryFindStandPosition(seat, config, out Vector3 freePosition))
                standPosition = freePosition;

            ExitSeat(seat, standPosition, config);
        }

        public void OnSeatLost(ISeat seat)
        {
            if (!ReferenceEquals(_seat, seat))
                return;

            ForceStand();
        }

        private void ExitSeat(ISeat seat, Vector3 standPosition, ChairSittingConfig config)
        {
            _seat = null;
            seat.Release(this);

            float yaw = transform.eulerAngles.y;
            transform.SetPositionAndRotation(standPosition, Quaternion.Euler(0f, yaw, 0f));

            if (body != null)
            {
                body.interpolation = _preSeatInterpolation;
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            if (bodyCollider != null)
                bodyCollider.enabled = true;

            if (look != null)
            {
                look.SetSeatedView(false, config.SeatedEyeHeight);
                look.ClearSeatFrame();
            }

            if (motor != null)
                motor.enabled = true;

            Physics.SyncTransforms();

            _resitBlockedUntil = Time.time + config.StandUpCooldown;
            SeatedChanged?.Invoke(seat, false);
        }

        private ChairSittingConfig ResolveConfig(ISeat seat)
        {
            if (seat.Config != null)
                return seat.Config;

            if (defaultConfig == null)
                defaultConfig = ScriptableObject.CreateInstance<ChairSittingConfig>();

            return defaultConfig;
        }

        private bool TryFindStandPosition(ISeat seat, ChairSittingConfig config, out Vector3 position)
        {
            position = _preSeatPosition;

            float radius = bodyCollider != null ? bodyCollider.radius * 0.9f : 0.27f;
            float height = bodyCollider != null ? bodyCollider.height : 1.8f;
            float distance = config.StandProbeDistance;

            Transform seatRoot = seat.SeatTransform;
            Vector3 seatPosition = seatRoot != null ? seatRoot.position : _preSeatPosition;

            Vector3 right = seatRoot != null
                ? Vector3.ProjectOnPlane(seatRoot.right, Vector3.up).normalized
                : transform.right;
            Vector3 forward = seatRoot != null
                ? Vector3.ProjectOnPlane(seatRoot.forward, Vector3.up).normalized
                : transform.forward;

            if (right.sqrMagnitude < 0.01f) right = transform.right;
            if (forward.sqrMagnitude < 0.01f) forward = transform.forward;

            Vector3[] offsets =
            {
                right * distance,
                -right * distance,
                forward * distance,
                -forward * distance
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 candidate = seatPosition + offsets[i];
                candidate.y = SampleGroundHeight(candidate, seatRoot) + config.StandProbeLift;

                if (IsCapsuleFree(candidate, radius, height, seatRoot, transform))
                {
                    position = candidate;
                    return true;
                }
            }

            // Last resort: where the player stood before sitting, if the seat has not blocked it.
            if (IsCapsuleFree(_preSeatPosition, radius, height, seatRoot, transform))
            {
                position = _preSeatPosition;
                return true;
            }

            return false;
        }

        private float SampleGroundHeight(Vector3 position, Transform seatRoot)
        {
            Vector3 origin = position + Vector3.up * 2f;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);

            float bestHeight = seatRoot != null ? seatRoot.position.y : position.y;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                    continue;

                if (IsOwnedBy(collider.transform, seatRoot) || IsOwnedBy(collider.transform, transform))
                    continue;

                if (hits[i].distance < bestDistance)
                {
                    bestDistance = hits[i].distance;
                    bestHeight = hits[i].point.y;
                }
            }

            return bestHeight;
        }

        private bool IsCapsuleFree(Vector3 feetPosition, float radius, float height, Transform seatRoot, Transform playerRoot)
        {
            Vector3 bottom = feetPosition + Vector3.up * radius;
            Vector3 top = feetPosition + Vector3.up * Mathf.Max(radius, height - radius);

            Collider[] overlaps = Physics.OverlapCapsule(
                bottom,
                top,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null)
                    continue;

                if (IsOwnedBy(collider.transform, seatRoot) || IsOwnedBy(collider.transform, playerRoot))
                    continue;

                return false;
            }

            return true;
        }

        private static bool IsOwnedBy(Transform candidate, Transform owner)
        {
            return owner != null && candidate != null && candidate.IsChildOf(owner);
        }

        private static string Describe(ISeat seat)
        {
            return seat is Component component ? component.name : "seat";
        }

        private void OnDisable()
        {
            if (_seat != null)
                ForceStand();
        }
    }
}
