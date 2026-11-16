using System;
using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// A physical fire extinguisher. It owns its fuel, its spray state and the hand attachment.
    /// Propulsion itself lives in <see cref="RocketChairController"/>; this component only knows
    /// how to be picked up, held and emptied.
    /// </summary>
    [DisallowMultipleComponent]
    public class FireExtinguisher : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] private string pickupPrompt = "Grab extinguisher";

        [Header("Hand Attachment")]
        [Tooltip("Local position while held by the player's hand anchor.")]
        [SerializeField] private Vector3 handLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 handLocalEulerAngles = new Vector3(0f, -20f, 0f);
        [Tooltip("View-model scale while held. The world prop keeps its normal scale.")]
        [SerializeField] private Vector3 handLocalScale = new Vector3(0.4f, 0.4f, 0.4f);

        [Header("Fuel")]
        [SerializeField, Min(1f)] private float fuelCapacity = 100f;
        [SerializeField, Min(0f)] private float consumptionPerSecond = 7.5f;
        [Tooltip("Off makes the extinguisher start empty, which is handy for testing the cutoff.")]
        [SerializeField] private bool startFull = true;

        [Header("References")]
        [SerializeField] private ExtinguisherVFX vfx;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider[] worldColliders;
        [SerializeField] private AudioSource dischargeSource;
        [SerializeField] private AudioSource startSource;
        [SerializeField] private AudioSource stopSource;

        [Header("Audio Clips (assign manually)")]
        [SerializeField] private AudioClip dischargeClip;
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip stopClip;

        private float _fuel;
        private bool _spraying;
        private bool _equipped;
        private RigidbodyInterpolation _preEquipInterpolation = RigidbodyInterpolation.Interpolate;

        public event Action SprayStarted;
        public event Action SprayStopped;

        public bool IsEquipped => _equipped;
        public bool IsSpraying => _spraying;
        public bool IsEmpty => _fuel <= 0f;
        public float Fuel => _fuel;
        public float FuelCapacity => fuelCapacity;
        public float Fuel01 => fuelCapacity > 0f ? Mathf.Clamp01(_fuel / fuelCapacity) : 0f;

        public string InteractionPrompt => pickupPrompt;

        private void Awake()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (vfx == null)
                vfx = GetComponentInChildren<ExtinguisherVFX>();
            if (worldColliders == null || worldColliders.Length == 0)
                worldColliders = GetComponentsInChildren<Collider>();

            _fuel = startFull ? fuelCapacity : 0f;
        }

        private void OnDisable()
        {
            StopSpray();
        }

        // ------------------------------------------------------------------ interaction

        public bool CanInteract(GameObject interactor)
        {
            if (_equipped || _fuel <= 0f || interactor == null)
                return false;

            return interactor.GetComponentInParent<IEquipmentHolder>() != null;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            IEquipmentHolder holder = interactor.GetComponentInParent<IEquipmentHolder>();
            holder?.TryEquip(this);
        }

        // ------------------------------------------------------------------ hand attachment

        /// <summary>Parents this extinguisher to a hand anchor and disables its world physics.</summary>
        public bool TryEquip(Transform anchor)
        {
            if (anchor == null || _equipped)
                return false;

            _equipped = true;

            if (body != null)
            {
                // The hand anchor owns the transform while the prop is carried. A rigidbody
                // (even a kinematic one) interpolates its rendered pose from physics history,
                // which leaves the prop lagging behind - or flat out staying behind - as the
                // player moves. Turning interpolation off lets the hierarchy drive it directly.
                _preEquipInterpolation = body.interpolation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.detectCollisions = false;
            }

            SetCollidersEnabled(false);

            transform.SetParent(anchor, false);
            transform.localPosition = handLocalPosition;
            transform.localRotation = Quaternion.Euler(handLocalEulerAngles);
            transform.localScale = handLocalScale;

            return true;
        }

        /// <summary>Puts the extinguisher back into the world as a physical prop.</summary>
        public void Detach(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (!_equipped)
                return;

            StopSpray();
            _equipped = false;

            transform.SetParent(null, true);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            transform.localScale = Vector3.one;

            SetCollidersEnabled(true);

            if (body != null)
            {
                body.isKinematic = false;
                body.detectCollisions = true;
                body.interpolation = _preEquipInterpolation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }

        private void SetCollidersEnabled(bool enabledState)
        {
            if (worldColliders == null)
                return;

            for (int i = 0; i < worldColliders.Length; i++)
            {
                if (worldColliders[i] != null)
                    worldColliders[i].enabled = enabledState;
            }
        }

        // ------------------------------------------------------------------ fuel and spray

        /// <summary>Starts spraying. True while the extinguisher is spraying or can keep spraying.</summary>
        public bool TryStartSpray()
        {
            if (IsEmpty)
                return false;
            if (_spraying)
                return true;

            _spraying = true;

            if (vfx != null)
                vfx.Play();

            if (dischargeSource != null)
            {
                dischargeSource.clip = dischargeClip;
                dischargeSource.loop = true;
                if (dischargeClip != null)
                    dischargeSource.Play();
            }

            if (startSource != null && startClip != null)
                startSource.PlayOneShot(startClip);

            SprayStarted?.Invoke();
            return true;
        }

        /// <summary>Burns fuel. Stops the spray automatically when the tank runs dry.</summary>
        public void Consume(float deltaTime)
        {
            if (!_spraying || deltaTime <= 0f)
                return;

            _fuel = Mathf.Max(0f, _fuel - consumptionPerSecond * deltaTime);

            if (_fuel <= 0f)
                StopSpray();
        }

        public void StopSpray()
        {
            if (!_spraying)
                return;

            _spraying = false;

            if (vfx != null)
                vfx.Stop();

            if (dischargeSource != null && dischargeSource.isPlaying)
                dischargeSource.Stop();

            if (stopSource != null && stopClip != null)
                stopSource.PlayOneShot(stopClip);

            SprayStopped?.Invoke();
        }

        /// <summary>Sets the remaining fuel. Used by refill pickups and automated tests.</summary>
        public void SetFuel(float amount)
        {
            _fuel = Mathf.Clamp(amount, 0f, fuelCapacity);

            if (_fuel <= 0f)
                StopSpray();
        }

        public void Refill()
        {
            SetFuel(fuelCapacity);
        }

        /// <summary>Lets the VFX switch on its high-speed smoke trail.</summary>
        public void SetTravelSpeed(float speed)
        {
            if (vfx != null)
                vfx.SetTravelSpeed(speed);
        }
    }
}
