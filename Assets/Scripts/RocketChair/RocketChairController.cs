using FireExtinguisher.Interaction;
using FireExtinguisher.Player;
using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// Turns a chair + a held extinguisher into a rocket. Thrust is applied at the chair's
    /// center of mass directly opposite the rider's view direction, so the spray acts like a
    /// thruster and the chair does not spin itself. A soft upright assist keeps normal flight
    /// controllable while still allowing big impacts to tip and tumble the chair.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(ChairSeat))]
    public class RocketChairController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ChairSeat seat;
        [SerializeField] private Rigidbody body;
        [SerializeField] private FunnyImpactEffects impacts;

        [Header("Mass")]
        [SerializeField, Min(0f)] private float emptyMass = 10f;
        [SerializeField, Min(0f)] private float riderMass = 65f;

        [Header("Thrust")]
        [Tooltip("Newtons applied through the center of mass, opposite the rider's view direction.")]
        [SerializeField, Min(0f)] private float thrustForce = 1800f;
        [SerializeField, Min(0.1f)] private float maxSpeed = 10f;

        [Header("Upright Assist")]
        [Tooltip("Angular acceleration used to keep the chair upright while coasting.")]
        [SerializeField, Min(0f)] private float uprightAcceleration = 50f;
        [Tooltip("Angular acceleration while firing. Full thrust pushes the chair hard enough " +
                 "to tip it over, so the assist has to beat the tipping moment of the wheels.")]
        [SerializeField, Min(0f)] private float thrustUprightAcceleration = 200f;
        [Tooltip("Tilt below this angle is left alone, so normal rolling never fights the assist.")]
        [SerializeField, Range(0f, 60f)] private float uprightDeadZone = 5f;
        [Tooltip("Tilt at which the assist reaches full strength.")]
        [SerializeField, Range(1f, 89f)] private float uprightFullTilt = 25f;
        [SerializeField, Min(0f)] private float uprightDamping = 4.5f;
        [SerializeField, Min(0f)] private float thrustUprightDamping = 12f;
        [Tooltip("Assist is suspended this long after a medium or big impact, so the chair can tip.")]
        [SerializeField, Min(0f)] private float impactRecoverySeconds = 1f;

        [Header("Camera Feedback")]
        [SerializeField, Range(0f, 1f)] private float firingShake = 1f;

        private PlayerInputReader _input;
        private PlayerEquipment _equipment;
        private Transform _camera;
        private Component _rider;
        private bool _hadRider;
        private bool _thrusting;
        private float _uprightBlockedUntil;

        public bool IsThrusting => _thrusting;
        public float CurrentSpeed => body != null ? body.linearVelocity.magnitude : 0f;

        private void Awake()
        {
            if (seat == null) seat = GetComponent<ChairSeat>();
            if (body == null) body = GetComponent<Rigidbody>();
            if (impacts == null) impacts = GetComponent<FunnyImpactEffects>();

            if (body != null)
                body.mass = emptyMass;
        }

        private void OnEnable()
        {
            if (impacts != null)
                impacts.Impacted += HandleImpact;
        }

        private void OnDisable()
        {
            if (impacts != null)
                impacts.Impacted -= HandleImpact;

            ReleaseRider();
        }

        private void FixedUpdate()
        {
            bool occupied = seat != null && seat.IsOccupied;

            if (occupied != _hadRider)
            {
                _hadRider = occupied;

                if (occupied)
                    ResolveRider();
                else
                    ReleaseRider();
            }

            if (!occupied || body == null)
                return;

            ApplyUprightAssist();
            ApplyThrust();

            ClampSpeed();

            if (_equipment != null && _equipment.Equipped != null)
                _equipment.Equipped.SetTravelSpeed(body.linearVelocity.magnitude);
        }

        // ------------------------------------------------------------------ rider

        private void ResolveRider()
        {
            _rider = seat.Occupant as Component;
            if (_rider == null)
                return;

            _input = _rider.GetComponent<PlayerInputReader>();
            _equipment = _rider.GetComponent<PlayerEquipment>();

            Camera riderCamera = _rider.GetComponentInChildren<Camera>(true);
            if (riderCamera != null)
            {
                _camera = riderCamera.transform;
            }
            else
            {
                PlayerLook look = _rider.GetComponent<PlayerLook>();
                _camera = look != null ? look.CameraPivot : null;
            }

            if (body != null)
                body.mass = emptyMass + riderMass;
        }

        private void ReleaseRider()
        {
            if (_equipment != null && _equipment.Equipped != null)
                _equipment.Equipped.StopSpray();

            CameraFeedback.SetRocketIntensity(0f);

            _input = null;
            _equipment = null;
            _camera = null;
            _rider = null;
            _thrusting = false;

            if (body != null)
                body.mass = emptyMass;
        }

        // ------------------------------------------------------------------ propulsion

        private void ApplyThrust()
        {
            FireExtinguisher extinguisher = _equipment != null ? _equipment.Equipped : null;
            bool wantsFire = _input != null && _input.FireHeld;

            bool spraying = false;

            if (wantsFire &&
                extinguisher != null &&
                extinguisher.IsEquipped &&
                !extinguisher.IsEmpty &&
                _camera != null)
            {
                if (extinguisher.TryStartSpray())
                {
                    // Center-of-mass force, opposite the view: the extinguisher sprays away
                    // from the rider, so the reaction pushes the chair the other way.
                    body.AddForce(-_camera.forward * thrustForce, ForceMode.Force);
                    extinguisher.Consume(Time.fixedDeltaTime);
                    spraying = true;
                }
            }

            if (!spraying && extinguisher != null)
                extinguisher.StopSpray();

            _thrusting = spraying;
            CameraFeedback.SetRocketIntensity(spraying ? firingShake : 0f);
        }

        private void ClampSpeed()
        {
            Vector3 velocity = body.linearVelocity;
            float speed = velocity.magnitude;

            if (speed > maxSpeed && speed > 0.001f)
                body.linearVelocity = velocity * (maxSpeed / speed);
        }

        private void ApplyUprightAssist()
        {
            if (Time.time < _uprightBlockedUntil)
                return;

            Vector3 up = body.rotation * Vector3.up;
            float tilt = Vector3.Angle(up, Vector3.up);

            if (tilt <= uprightDeadZone)
                return;

            Vector3 axis = Vector3.Cross(up, Vector3.up);
            if (axis.sqrMagnitude < 0.0001f)
                return;

            float span = Mathf.Max(1f, uprightFullTilt - uprightDeadZone);
            float strength = Mathf.Clamp01((tilt - uprightDeadZone) / span);

            // Firing pushes the chair hard enough to flip it, so the assist is much stronger
            // while the extinguisher runs. Coasting stays soft and tippy.
            float acceleration = _thrusting
                ? Mathf.Max(uprightAcceleration, thrustUprightAcceleration)
                : uprightAcceleration;
            float damping = _thrusting
                ? Mathf.Max(uprightDamping, thrustUprightDamping)
                : uprightDamping;

            body.AddTorque(axis.normalized * (acceleration * strength), ForceMode.Acceleration);

            // Damp the tipping rotation only. Yaw spin is left alone so the chair can still drift.
            Vector3 horizontalSpin = Vector3.ProjectOnPlane(body.angularVelocity, Vector3.up);
            body.AddTorque(-horizontalSpin * damping, ForceMode.Acceleration);
        }

        private void HandleImpact(int level, float speed)
        {
            if (level < 2)
                return;

            _uprightBlockedUntil = Time.time + impactRecoverySeconds;
        }

        private void Reset()
        {
            seat = GetComponent<ChairSeat>();
            body = GetComponent<Rigidbody>();
            impacts = GetComponent<FunnyImpactEffects>();
        }

        private void OnDrawGizmosSelected()
        {
            if (body == null)
                return;

            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            Gizmos.DrawSphere(body.worldCenterOfMass, 0.06f);
        }
    }
}
