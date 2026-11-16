using System;
using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// A glass pane that behaves like a wall until something slams into it hard enough, then
    /// swaps itself for a pile of physical shards. Impact is scored the same way as
    /// <see cref="FunnyImpactEffects"/> (closing speed times a mass factor), so a heavy chair
    /// breaks it at a lower speed than a person walking into it.
    /// </summary>
    [DisallowMultipleComponent]
    public class BreakableGlass : MonoBehaviour
    {
        [Header("Impact Threshold")]
        [Tooltip("Closing speed (m/s) below which the pane never breaks, however heavy the impactor.")]
        [SerializeField, Min(0f)] private float minimumSpeed = 4f;
        [Tooltip("Impact score (speed x mass factor) needed to shatter the pane.")]
        [SerializeField, Min(0f)] private float breakScore = 15f;

        [Header("Pane")]
        [Tooltip("This pane's collider. Disabled the moment the glass shatters.")]
        [SerializeField] private Collider paneCollider;
        [Tooltip("This pane's renderer. Hidden the moment the glass shatters.")]
        [SerializeField] private Renderer paneRenderer;
        [Tooltip("Parent of the inactive shard objects, activated on break.")]
        [SerializeField] private GameObject shardsRoot;

        [Header("Shatter")]
        [SerializeField, Min(0f)] private float shardLifetime = 6f;
        [Tooltip("Initial shard speed in m/s away from the impact point.")]
        [SerializeField, Min(0f)] private float shardSpeed = 2.4f;
        [SerializeField, Min(0f)] private float shardSpin = 9f;
        [SerializeField, Min(0f)] private float cameraTrauma = 0.45f;
        [SerializeField, Min(1f)] private float shakeRange = 20f;

        [Header("Audio (assign clip manually)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip breakClip;

        /// <summary>Raised once, right after the pane shatters.</summary>
        public event Action Broken;

        public bool IsBroken { get; private set; }
        public int ShardCount => shardsRoot != null ? shardsRoot.transform.childCount : 0;
        public Transform ShardsRoot => shardsRoot != null ? shardsRoot.transform : null;

        private void Awake()
        {
            if (paneCollider == null)
                paneCollider = GetComponent<Collider>();
            if (paneRenderer == null)
                paneRenderer = GetComponent<Renderer>();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (shardsRoot != null)
                shardsRoot.SetActive(false);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsBroken || collision.contactCount == 0)
                return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minimumSpeed)
                return;

            Rigidbody impactor = collision.rigidbody;
            float massFactor = impactor != null
                ? Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.1f, impactor.mass) / 10f), 0.5f, 2f)
                : 1f;

            if (speed * massFactor < breakScore)
                return;

            ContactPoint contact = collision.GetContact(0);
            Break(contact.point, collision.relativeVelocity.normalized);
        }

        /// <summary>
        /// Shatters the pane. Called by hard hits and usable directly by tests and tools.
        /// </summary>
        public bool Break(Vector3 point, Vector3 impactDirection)
        {
            if (IsBroken)
                return false;

            IsBroken = true;

            if (paneCollider != null)
                paneCollider.enabled = false;
            if (paneRenderer != null)
                paneRenderer.enabled = false;

            ScatterShards(point, impactDirection);
            PlayBreakSound();
            ShakeCamera(point);

            Broken?.Invoke();
            return true;
        }

        private void ScatterShards(Vector3 point, Vector3 impactDirection)
        {
            if (shardsRoot == null)
                return;

            shardsRoot.SetActive(true);

            Vector3 push = impactDirection.sqrMagnitude > 0.0001f
                ? impactDirection.normalized
                : transform.forward;

            foreach (Transform shard in shardsRoot.transform)
            {
                shard.gameObject.SetActive(true);

                Rigidbody shardBody = shard.GetComponent<Rigidbody>();
                if (shardBody != null)
                {
                    // Shards fly with the impact, drift away from the hole and lift a little so
                    // the burst reads even when the chair hits the pane dead centre.
                    Vector3 spread = shard.position - point;
                    spread.y = Mathf.Abs(spread.y) * 0.35f + 0.25f;
                    spread = spread.sqrMagnitude > 0.0001f ? spread.normalized : Vector3.up;

                    Vector3 velocity = (push * 0.85f + spread) * shardSpeed;
                    shardBody.linearVelocity = velocity;
                    shardBody.angularVelocity = UnityEngine.Random.insideUnitSphere * shardSpin;
                    shardBody.WakeUp();
                }

                UnityEngine.Object.Destroy(shard.gameObject, shardLifetime);
            }
        }

        private void PlayBreakSound()
        {
            if (audioSource != null && breakClip != null)
                audioSource.PlayOneShot(breakClip);
        }

        private void ShakeCamera(Vector3 point)
        {
            Camera viewCamera = Camera.main;
            if (viewCamera == null)
                return;

            float distance = Vector3.Distance(viewCamera.transform.position, point);
            float attenuation = Mathf.Clamp01(1f - distance / Mathf.Max(1f, shakeRange));
            if (attenuation <= 0f)
                return;

            CameraFeedback.AddTrauma(cameraTrauma * attenuation);
        }

        /// <summary>True when the glass would break for the given impact. Used by tools and tests.</summary>
        public bool WouldBreak(float speed, float impactorMass)
        {
            if (IsBroken || speed < minimumSpeed)
                return false;

            float massFactor = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.1f, impactorMass) / 10f), 0.5f, 2f);
            return speed * massFactor >= breakScore;
        }
    }
}
