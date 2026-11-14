using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Rigidbody-based first-person movement. Disabling this component is how the chair
    /// system turns normal movement off while seated.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Rigidbody body;
        [SerializeField] private CapsuleCollider capsule;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0f)] private float acceleration = 22f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.25f;

        [Header("Jumping")]
        [SerializeField, Min(0f)] private float jumpSpeed = 4.6f;
        [SerializeField, Min(0f)] private float groundCheckDistance = 0.15f;
        [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 50f;
        [SerializeField] private LayerMask groundMask = ~0;

        private bool _grounded;
        private bool _jumpQueued;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (body == null) body = GetComponent<Rigidbody>();
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
        }

        private void Update()
        {
            if (input != null && input.JumpWasPressed)
                _jumpQueued = true;
        }

        private void FixedUpdate()
        {
            if (body == null || capsule == null || input == null)
                return;

            _grounded = CheckGrounded();

            if (_jumpQueued)
            {
                _jumpQueued = false;
                if (_grounded)
                {
                    Vector3 velocity = body.linearVelocity;
                    body.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
                    body.AddForce(Vector3.up * jumpSpeed, ForceMode.VelocityChange);
                    _grounded = false;
                }
            }

            Vector2 move = input.Move;
            Vector3 wishDirection = transform.right * move.x + transform.forward * move.y;
            wishDirection = Vector3.ClampMagnitude(wishDirection, 1f);

            float speed = input.SprintHeld ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = wishDirection * speed;

            Vector3 currentVelocity = body.linearVelocity;
            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            float forceScale = _grounded ? acceleration : acceleration * airControl;

            body.AddForce((targetVelocity - horizontalVelocity) * forceScale, ForceMode.Acceleration);
        }

        private bool CheckGrounded()
        {
            Vector3 origin = body.position + body.rotation * capsule.center;
            float distance = capsule.height * 0.5f + groundCheckDistance;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            // A ray starting inside our own capsule ignores it, but an object we are standing
            // on can still be part of this hierarchy (for example a child collider).
            if (hit.collider.transform.IsChildOf(transform))
                return false;

            return Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle;
        }
    }
}
