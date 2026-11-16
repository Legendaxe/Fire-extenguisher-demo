using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// Camera-local shake layered on top of the existing look controller. It never touches the
    /// camera pivot or the player body; it only offsets the camera transform in LateUpdate and
    /// smoothly returns to rest. Two sources: impact trauma and a continuous rocket rumble.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFeedback : MonoBehaviour
    {
        public static CameraFeedback Instance { get; private set; }

        [Header("Impact Shake")]
        [SerializeField, Min(0f)] private float impactOffset = 0.07f;
        [SerializeField, Min(0f)] private float impactAngle = 2.4f;
        [SerializeField, Min(0f)] private float traumaDecay = 1.7f;

        [Header("Rocket Rumble")]
        [Tooltip("Subtle: this shake is on for the whole burn and must not hurt aiming.")]
        [SerializeField, Min(0f)] private float rocketOffset = 0.008f;
        [SerializeField, Min(0f)] private float rocketAngle = 0.28f;
        [SerializeField, Min(0f)] private float rocketBlendSpeed = 5f;

        [Header("Noise")]
        [SerializeField, Min(0f)] private float positionFrequency = 24f;
        [SerializeField, Min(0f)] private float rotationFrequency = 19f;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private float _trauma;
        private float _rocket;
        private float _rocketTarget;
        private float _seed;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
            _seed = Random.Range(0f, 100f);
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;

            transform.localPosition = _baseLocalPosition;
            transform.localRotation = _baseLocalRotation;
        }

        /// <summary>Adds impact shake. 1 is a huge, screen-filling crash.</summary>
        public static void AddTrauma(float amount)
        {
            if (Instance == null || amount <= 0f)
                return;

            Instance._trauma = Mathf.Clamp01(Instance._trauma + amount);
        }

        /// <summary>Sets the continuous rumble while the rocket burns. 0..1.</summary>
        public static void SetRocketIntensity(float intensity)
        {
            if (Instance == null)
                return;

            Instance._rocketTarget = Mathf.Clamp01(intensity);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;

            _rocket = Mathf.MoveTowards(_rocket, _rocketTarget, rocketBlendSpeed * deltaTime);
            _trauma = Mathf.MoveTowards(_trauma, 0f, traumaDecay * deltaTime);

            float impact = _trauma * _trauma;
            float amplitude = impact * impactOffset + _rocket * rocketOffset;
            float angle = impact * impactAngle + _rocket * rocketAngle;

            if (amplitude <= 0.00001f && angle <= 0.0001f)
            {
                transform.localPosition = _baseLocalPosition;
                transform.localRotation = _baseLocalRotation;
                return;
            }

            float positionTime = Time.unscaledTime * positionFrequency + _seed;
            Vector3 offset = new Vector3(
                Noise(positionTime, 0.15f),
                Noise(positionTime + 11.7f, 3.2f),
                Noise(positionTime + 23.1f, 7.4f)) * amplitude;

            float rotationTime = Time.unscaledTime * rotationFrequency + _seed;
            Vector3 euler = new Vector3(
                Noise(rotationTime, 1.1f),
                Noise(rotationTime + 5.3f, 2.7f),
                Noise(rotationTime + 9.9f, 4.4f)) * angle;

            transform.localPosition = _baseLocalPosition + offset;
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(euler);
        }

        private static float Noise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }
    }
}
