using UnityEngine;

namespace FireExtinguisher.Rocket
{
    /// <summary>
    /// One-shot cartoon collision burst. Spawned by <see cref="FunnyImpactEffects"/> at the
    /// contact point, plays puff and star particles plus a short flash, then removes itself.
    /// </summary>
    [DisallowMultipleComponent]
    public class FunnyImpactFX : MonoBehaviour
    {
        [Header("Particles")]
        [SerializeField] private ParticleSystem puff;
        [SerializeField] private ParticleSystem stars;

        [Header("Flash")]
        [SerializeField] private Light flash;
        [SerializeField, Min(0f)] private float flashDuration = 0.12f;
        [SerializeField, Min(0f)] private float flashIntensity = 8f;

        [Header("Lifetime")]
        [SerializeField, Min(0.2f)] private float destroyAfter = 2.5f;

        private float _flashOffTime;

        public void Play(int level)
        {
            level = Mathf.Clamp(level, 1, 3);

            int puffCount = level == 1 ? 12 : level == 2 ? 30 : 70;
            int starCount = level == 1 ? 3 : level == 2 ? 12 : 30;

            if (puff != null)
            {
                puff.Clear(true);
                puff.Play(true);
                puff.Emit(puffCount);
            }

            if (stars != null)
            {
                stars.Clear(true);
                stars.Play(true);
                stars.Emit(starCount);
            }

            if (flash != null)
            {
                flash.enabled = true;
                flash.intensity = flashIntensity * (0.5f + 0.5f * level);
                _flashOffTime = Time.time + flashDuration;
            }

            Destroy(gameObject, destroyAfter);
        }

        private void Update()
        {
            if (flash != null && flash.enabled && Time.time >= _flashOffTime)
                flash.enabled = false;
        }
    }
}
