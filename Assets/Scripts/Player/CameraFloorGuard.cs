using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Keeps the first-person camera out of the floor while the chair tips and rolls. The seated
    /// view follows the chair's full rotation, so a chair on its side or upside down can push the
    /// camera under the floor (or cut the floor with the near plane). This guard runs after every
    /// other LateUpdate - <see cref="SeatController"/> and <see cref="Rocket.CameraFeedback"/> -
    /// and lifts the camera to the surface below it plus the near-plane clearance.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraFloorGuard : MonoBehaviour
    {
        [Header("Probe")]
        [Tooltip("How far above the camera the downward probe starts. Covers a camera well under the floor.")]
        [SerializeField, Min(0f)] private float probeUp = 3f;

        [Tooltip("How far below the camera the probe reaches.")]
        [SerializeField, Min(0f)] private float probeDown = 6f;

        [Tooltip("Extra space kept between the near clip plane and the ground.")]
        [SerializeField, Min(0f)] private float clearance = 0.25f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private Camera _camera;
        private SeatController _seatController;
        private Transform _seatRoot;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _seatController = GetComponentInParent<SeatController>();
        }

        private void LateUpdate()
        {
            ApplyGuard();
        }

        /// <summary>
        /// Lifts the camera above the nearest surface below it. Public so the smoke test can
        /// drive the same clamp deterministically.
        /// </summary>
        public void ApplyGuard()
        {
            _seatRoot = _seatController != null && _seatController.CurrentSeat != null
                ? _seatController.CurrentSeat.SeatTransform
                : null;

            Vector3 position = transform.position;
            float minimumY = FindGroundHeight(position) + GetClearance();
            if (position.y >= minimumY)
                return;

            transform.position = new Vector3(position.x, minimumY, position.z);
        }

        private float FindGroundHeight(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * probeUp;
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, _hits, probeUp + probeDown, ~0, QueryTriggerInteraction.Ignore);

            float below = 0f;
            float belowDistance = float.MaxValue;
            float any = 0f;
            float anyDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hits[i].collider;
                if (collider == null || ShouldIgnore(collider))
                    continue;

                float height = _hits[i].point.y;

                if (_hits[i].distance < anyDistance)
                {
                    anyDistance = _hits[i].distance;
                    any = height;
                }

                // Only surfaces at or below the eye count as ground; otherwise a ceiling inside
                // the probe range would shove the camera up against it.
                if (height <= position.y + 0.01f && _hits[i].distance < belowDistance)
                {
                    belowDistance = _hits[i].distance;
                    below = height;
                }
            }

            // No surface below the eye means the eye is already under the floor: the first hit
            // above it is the floor, and lifting to that is exactly what is needed.
            return belowDistance < float.MaxValue ? below : any;
        }

        private bool ShouldIgnore(Collider collider)
        {
            Transform candidate = collider.transform;

            // The rider and the chair they sit on are always directly under the camera; they are
            // not ground, and treating them as ground would push the eye up with every bounce.
            if (_seatController != null && candidate.IsChildOf(_seatController.transform))
                return true;

            return _seatRoot != null && candidate.IsChildOf(_seatRoot);
        }

        private float GetClearance()
        {
            float near = _camera != null ? _camera.nearClipPlane : 0.1f;
            return near + clearance;
        }
    }
}
