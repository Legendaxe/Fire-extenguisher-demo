using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// Drives the extinguisher's particle systems: the continuous powder stream, a short
    /// starting burst and an optional smoke trail while the chair is moving fast.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExtinguisherVFX : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private ParticleSystem stream;
        [SerializeField] private ParticleSystem burst;
        [SerializeField] private ParticleSystem trail;

        [Header("Aim")]
        [Tooltip("The spray is fired along this transform's forward axis. Empty resolves to " +
                 "the main camera, so the powder always leaves the nozzle toward where the " +
                 "player is looking.")]
        [SerializeField] private Transform aimSource;

        [Header("Burst")]
        [SerializeField, Min(0)] private int burstCount = 26;

        [Header("Smoke Trail")]
        [SerializeField, Min(0f)] private float trailSpeedThreshold = 4f;
        [SerializeField, Min(0f)] private float trailRate = 20f;

        private bool _playing;
        private bool _trailActive;
        private float _travelSpeed;

        public bool IsPlaying => _playing;

        private void Awake()
        {
            if (aimSource == null && Camera.main != null)
                aimSource = Camera.main.transform;

            ApplyTrailState(false);
        }

        public void Play()
        {
            if (_playing)
                return;

            _playing = true;
            AlignEmissionToNozzle();

            if (stream != null)
                stream.Play(true);

            if (burst != null)
            {
                burst.Clear(true);
                burst.Play(true);
                burst.Emit(burstCount);
            }

            ApplyTrailState(false);
        }

        private void LateUpdate()
        {
            if (_playing)
                AlignEmissionToNozzle();
        }

        /// <summary>
        /// The spray uses world simulation so the powder stays behind the chair. Particles are
        /// spawned along the emitter's rotation combined with the shape's rotation, so writing
        /// the emitter's world rotation into the shape (what this used to do) aimed the cone at
        /// twice the prop's rotation. Converts the desired world direction into emitter-local
        /// space instead, so the cone leaves the nozzle along the aim source.
        /// </summary>
        private void AlignEmissionToNozzle()
        {
            if (aimSource == null && Camera.main != null)
                aimSource = Camera.main.transform;

            Vector3 direction = aimSource != null ? aimSource.forward : transform.forward;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Vector3 up = aimSource != null ? aimSource.up : transform.up;
            Quaternion desired = Quaternion.LookRotation(direction, up);
            Vector3 localEuler = (Quaternion.Inverse(transform.rotation) * desired).eulerAngles;

            AlignShape(stream, localEuler);
            AlignShape(burst, localEuler);
        }

        private static void AlignShape(ParticleSystem system, Vector3 euler)
        {
            if (system == null)
                return;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.rotation = euler;
        }

        public void Stop()
        {
            if (!_playing)
                return;

            _playing = false;

            if (stream != null)
                stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (burst != null)
                burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ApplyTrailState(false);
        }

        public void SetTravelSpeed(float speed)
        {
            _travelSpeed = speed;
            ApplyTrailState(_playing && speed >= trailSpeedThreshold);
        }

        private void ApplyTrailState(bool active)
        {
            if (trail == null)
                return;

            ParticleSystem.EmissionModule emission = trail.emission;
            emission.rateOverTime = active ? trailRate : 0f;

            if (active == _trailActive)
                return;

            _trailActive = active;

            if (active)
                trail.Play(true);
            else
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void OnDisable()
        {
            _playing = false;
            _trailActive = false;
        }
    }
}
