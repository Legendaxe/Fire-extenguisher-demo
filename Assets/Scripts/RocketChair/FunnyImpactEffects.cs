using System;
using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// Reusable collision feedback for any physics prop. Scores a collision by relative speed
    /// and the other body's mass, then plays a cartoon burst, a sound and camera shake.
    /// Per-instance cooldown plus pair de-duplication keep impacts readable and spam-free.
    /// An optional <see cref="IImpactGate"/> can veto feedback, which is how a chair nobody is
    /// riding stays silent when it is pushed into something.
    /// </summary>
    [DisallowMultipleComponent]
    public class FunnyImpactEffects : MonoBehaviour
    {
        [Header("Impact Scoring (m/s)")]
        [SerializeField, Min(0f)] private float minimumSpeed = 3f;
        [SerializeField, Min(0f)] private float mediumSpeed = 6f;
        [SerializeField, Min(0f)] private float bigSpeed = 9f;

        [Header("Spam Control")]
        [SerializeField, Min(0f)] private float cooldown = 0.35f;
        [Tooltip("Only the lower-instance object reacts when both colliders carry this component.")]
        [SerializeField] private bool suppressDuplicatePairs = true;

        [Header("Trigger Gate")]
        [Tooltip("Optional component implementing IImpactGate. While its ImpactAllowed is false " +
                 "the prop stays silent, so an empty chair pushed into a desk does not fire " +
                 "feedback at the player who is only walking into it.")]
        [SerializeField] private MonoBehaviour gateSource;

        [Header("Visual Effect")]
        [SerializeField] private FunnyImpactFX fxPrefab;
        [SerializeField] private Transform fxParent;

        [Header("Audio (assign clips manually)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip smallClip;
        [SerializeField] private AudioClip mediumClip;
        [SerializeField] private AudioClip bigClip;

        [Header("Camera Shake")]
        [SerializeField] private float smallTrauma = 0.18f;
        [SerializeField] private float mediumTrauma = 0.38f;
        [SerializeField] private float bigTrauma = 0.85f;
        [SerializeField, Min(1f)] private float shakeRange = 20f;

        private float _nextAllowedTime;
        private IImpactGate _gate;

        /// <summary>Level 1..3 plus the scored impact speed.</summary>
        public event Action<int, float> Impacted;

        public int ImpactCount { get; private set; }
        public int LastImpactLevel { get; private set; }
        public float LastImpactSpeed { get; private set; }
        public bool IsOnCooldown => Time.time < _nextAllowedTime;

        /// <summary>True when the gate (if any) currently allows impact feedback.</summary>
        public bool ImpactsAllowed => _gate == null || _gate.ImpactAllowed;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            _gate = gateSource as IImpactGate;
            if (gateSource != null && _gate == null)
            {
                Debug.LogWarning(
                    $"{nameof(FunnyImpactEffects)} on '{name}' has a gate source that does not " +
                    $"implement {nameof(IImpactGate)}; impacts stay enabled.", this);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < _nextAllowedTime || collision.contactCount == 0)
                return;

            if (_gate != null && !_gate.ImpactAllowed)
                return;

            if (suppressDuplicatePairs && collision.collider != null)
            {
                FunnyImpactEffects other = collision.collider.GetComponentInParent<FunnyImpactEffects>();
                if (other != null && other != this && other.GetInstanceID() < GetInstanceID())
                    return;
            }

            float speed = collision.relativeVelocity.magnitude;
            Rigidbody otherBody = collision.rigidbody;
            float massFactor = otherBody != null
                ? Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.1f, otherBody.mass) / 10f), 0.5f, 2f)
                : 1f;

            float score = speed * massFactor;
            int level = ScoreToLevel(score);

            if (level <= 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            Trigger(level, score, contact.point, contact.normal);
        }

        private int ScoreToLevel(float score)
        {
            if (score >= bigSpeed) return 3;
            if (score >= mediumSpeed) return 2;
            if (score >= minimumSpeed) return 1;
            return 0;
        }

        private void Trigger(int level, float speed, Vector3 point, Vector3 normal)
        {
            _nextAllowedTime = Time.time + cooldown;
            ImpactCount++;
            LastImpactLevel = level;
            LastImpactSpeed = speed;

            SpawnFx(level, point, normal);
            PlayClip(level);
            AddCameraShake(level, point);

            Impacted?.Invoke(level, speed);
        }

        private void SpawnFx(int level, Vector3 point, Vector3 normal)
        {
            if (fxPrefab == null)
                return;

            Quaternion rotation = normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(normal)
                : Quaternion.identity;

            FunnyImpactFX fx = Instantiate(fxPrefab, point, rotation, fxParent);
            fx.Play(level);
        }

        private void PlayClip(int level)
        {
            if (audioSource == null)
                return;

            AudioClip clip = level == 1 ? smallClip : level == 2 ? mediumClip : bigClip;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        private void AddCameraShake(int level, Vector3 point)
        {
            Camera viewCamera = Camera.main;
            float attenuation = 1f;

            if (viewCamera != null)
            {
                float distance = Vector3.Distance(viewCamera.transform.position, point);
                attenuation = Mathf.Clamp01(1f - distance / Mathf.Max(1f, shakeRange));
            }

            if (attenuation <= 0f)
                return;

            float trauma = level == 1 ? smallTrauma : level == 2 ? mediumTrauma : bigTrauma;
            CameraFeedback.AddTrauma(trauma * attenuation);
        }

        /// <summary>
        /// Fires a canned impact level. Used by the context menu and automated tests; it bypasses
        /// the impact gate on purpose so the effect can be previewed on an empty prop.
        /// </summary>
        public void PlayTestImpact(int level)
        {
            level = Mathf.Clamp(level, 1, 3);
            Vector3 point = transform.position + Vector3.up * 0.5f + transform.forward * 0.4f;
            Trigger(level, level * 3f, point, transform.forward);
        }

        [ContextMenu("Test Small Impact")]
        private void TestSmallImpact() => PlayTestImpact(1);

        [ContextMenu("Test Medium Impact")]
        private void TestMediumImpact() => PlayTestImpact(2);

        [ContextMenu("Test Big Impact")]
        private void TestBigImpact() => PlayTestImpact(3);

        private void OnDisable()
        {
            _nextAllowedTime = 0f;
        }
    }
}
