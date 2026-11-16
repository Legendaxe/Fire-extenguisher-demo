using FireExtinguisher.Interaction;
using UnityEngine;
using ExtinguisherItem = FireExtinguisher.Rocket.FireExtinguisher;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Owns the player's hand anchor and the prop carried in it. This is the only player-side
    /// equipment code; chairs and world props talk to it through <see cref="IEquipmentHolder"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEquipment : MonoBehaviour, IEquipmentHolder
    {
        [Header("References")]
        [Tooltip("Anchor under the camera pivot that holds the first-person prop.")]
        [SerializeField] private Transform handAnchor;
        [Tooltip("Player input source. Resolved on the same GameObject when left empty.")]
        [SerializeField] private PlayerInputReader input;

        [Header("Drop")]
        [SerializeField, Min(0f)] private float dropForwardDistance = 0.6f;
        [SerializeField, Min(0f)] private float dropHeight = 0.8f;

        private ExtinguisherItem _equipped;

        public Transform HandAnchor => handAnchor;
        public ExtinguisherItem Equipped => _equipped;
        public bool HasEquipment => _equipped != null;

        private void Awake()
        {
            if (handAnchor == null)
                handAnchor = FindHandAnchor();

            if (input == null)
                input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (input != null && input.DropWasPressed)
                Unequip();
        }

        private Transform FindHandAnchor()
        {
            Transform pivot = transform.Find("CameraPivot");
            if (pivot != null)
            {
                Transform anchor = pivot.Find("HandAnchor");
                if (anchor != null)
                    return anchor;
            }

            return null;
        }

        public bool TryEquip(ExtinguisherItem extinguisher)
        {
            if (extinguisher == null || extinguisher == _equipped)
                return false;

            if (handAnchor == null)
            {
                Debug.LogWarning($"{nameof(PlayerEquipment)}: no hand anchor assigned.", this);
                return false;
            }

            if (_equipped != null)
                Unequip();

            if (!extinguisher.TryEquip(handAnchor))
                return false;

            _equipped = extinguisher;
            return true;
        }

        public void Unequip()
        {
            if (_equipped == null)
                return;

            ExtinguisherItem extinguisher = _equipped;
            _equipped = null;

            Vector3 position = transform.position + transform.forward * dropForwardDistance + Vector3.up * dropHeight;
            extinguisher.Detach(position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }
    }
}
