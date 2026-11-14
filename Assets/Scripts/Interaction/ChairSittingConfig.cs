using UnityEngine;

namespace FireExtinguisher.Interaction
{
    /// <summary>
    /// Shared, per-chair tuning for sitting: prompts, seated view and stand-up probe.
    /// </summary>
    [CreateAssetMenu(menuName = "Fire Extinguisher/Chair Sitting Config", fileName = "ChairSittingConfig")]
    public class ChairSittingConfig : ScriptableObject
    {
        [Header("Prompts")]
        [SerializeField] private string sitPrompt = "Sit";
        [SerializeField] private string standPrompt = "Stand";

        [Header("Seated View")]
        [Tooltip("Camera height above the seat anchor, in metres.")]
        [SerializeField, Min(0f)] private float seatedEyeHeight = 0.68f;

        [Tooltip("How far the player may turn their head away from the chair's forward axis, in degrees.")]
        [SerializeField, Range(0f, 180f)] private float seatedYawLimit = 80f;

        [Header("Staying Seated")]
        [SerializeField, Min(0f)] private float standUpCooldown = 0.25f;

        [Header("Standing Up")]
        [Tooltip("Distance from the chair root at which the stand-up capsule is tested.")]
        [SerializeField, Min(0.1f)] private float standProbeDistance = 0.75f;

        [Tooltip("Extra clearance added above the floor when testing the stand-up capsule.")]
        [SerializeField, Min(0f)] private float standProbeLift = 0.05f;

        public string SitPrompt => sitPrompt;
        public string StandPrompt => standPrompt;
        public float SeatedEyeHeight => seatedEyeHeight;
        public float SeatedYawLimit => seatedYawLimit;
        public float StandUpCooldown => standUpCooldown;
        public float StandProbeDistance => standProbeDistance;
        public float StandProbeLift => standProbeLift;
    }
}
