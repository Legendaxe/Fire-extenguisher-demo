using UnityEngine;
using UnityEngine.InputSystem;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// First-person look. Standing, yaw rotates the body (through the Rigidbody) and pitch
    /// rotates the camera pivot. While seated the chair's own rotation drives the body; look
    /// input adds a clamped yaw offset on top of it, so the view rides the chair as it tips.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerLook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Rigidbody body;

        [Header("Sensitivity")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0f)] private float stickSensitivity = 180f;

        [Header("Pitch Limits")]
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Cursor")]
        [SerializeField] private bool lockCursorOnStart = true;

        private float _yaw;
        private float _pitch;
        private float _standingEyeHeight;
        private float _seatedYawOffset;
        private float _seatedYawLimit = 80f;
        private Transform _seatFrame;

        public Transform CameraPivot => cameraPivot;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (body == null) body = GetComponent<Rigidbody>();
            if (cameraPivot == null) cameraPivot = FindCameraPivot();

            _yaw = transform.eulerAngles.y;
            _standingEyeHeight = cameraPivot != null ? cameraPivot.localPosition.y : 1.6f;
        }

        private void OnEnable()
        {
            if (lockCursorOnStart)
                SetCursorLocked(true);
        }

        private void Update()
        {
            HandleCursorToggle();

            if (input == null || cameraPivot == null)
                return;

            Vector2 look = input.Look;
            if (look.sqrMagnitude <= 0f)
                return;

            float yawDelta;
            float pitchDelta;

            if (input.LookFromPointer)
            {
                // Mouse/pen deltas are already frame-relative.
                yawDelta = look.x * mouseSensitivity;
                pitchDelta = -look.y * mouseSensitivity;
            }
            else
            {
                // Stick input is a rate in [-1, 1] and needs delta time.
                float stick = stickSensitivity * Time.unscaledDeltaTime;
                yawDelta = look.x * stick;
                pitchDelta = -look.y * stick;
            }

            ApplyYaw(yawDelta);
            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void FixedUpdate()
        {
            // While seated the seat drives the body rotation, including the chair's tilt.
            if (_seatFrame != null)
                return;

            if (body == null || body.isKinematic)
            {
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                return;
            }

            body.MoveRotation(Quaternion.Euler(0f, _yaw, 0f));
        }

        /// <summary>Seats the view on a chair anchor and limits yaw around its up axis.</summary>
        public void SetSeatFrame(Transform frame, float yawLimit)
        {
            _seatFrame = frame;
            _seatedYawLimit = Mathf.Clamp(yawLimit, 0f, 180f);

            if (_seatFrame == null)
            {
                _seatedYawOffset = 0f;
                return;
            }

            // Keep the current look direction relative to the chair instead of snapping the view
            // to its forward axis. The offset is measured in the seat's frame so it survives
            // the chair spinning and tumbling.
            _seatedYawOffset = Mathf.Clamp(
                Mathf.DeltaAngle(_seatFrame.eulerAngles.y, transform.eulerAngles.y),
                -_seatedYawLimit,
                _seatedYawLimit);
        }

        /// <summary>
        /// Rotation the seated player root should use: the chair's full orientation plus the
        /// player's clamped look offset. Used by <see cref="SeatController"/> while riding.
        /// </summary>
        public Quaternion GetSeatedRotation()
        {
            if (_seatFrame == null)
                return transform.rotation;

            return _seatFrame.rotation * Quaternion.AngleAxis(_seatedYawOffset, Vector3.up);
        }

        public void ClearSeatFrame()
        {
            _seatFrame = null;
            _seatedYawOffset = 0f;

            // Pick up the yaw the seated view ended on so standing up does not snap the view.
            _yaw = transform.eulerAngles.y;
        }

        /// <summary>Moves the camera between standing and seated eye heights.</summary>
        public void SetSeatedView(bool seated, float seatedEyeHeight)
        {
            if (cameraPivot == null)
                return;

            Vector3 localPosition = cameraPivot.localPosition;
            localPosition.y = seated ? seatedEyeHeight : _standingEyeHeight;
            cameraPivot.localPosition = localPosition;
        }

        private void ApplyYaw(float yawDelta)
        {
            if (_seatFrame != null)
            {
                _seatedYawOffset = Mathf.Clamp(
                    _seatedYawOffset + yawDelta,
                    -_seatedYawLimit,
                    _seatedYawLimit);
                return;
            }

            _yaw = Mathf.Repeat(_yaw + yawDelta, 360f);
        }

        private Transform FindCameraPivot()
        {
            Transform pivot = transform.Find("CameraPivot");
            if (pivot != null)
                return pivot;

            Camera childCamera = GetComponentInChildren<Camera>();
            return childCamera != null ? childCamera.transform : null;
        }

        private void HandleCursorToggle()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (Mouse.current != null &&
                     Mouse.current.leftButton.wasPressedThisFrame &&
                     Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
