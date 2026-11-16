using UnityEngine;
using ExtinguisherItem = FireExtinguisher.Rocket.FireExtinguisher;

namespace FireExtinguisher.Interaction
{
    /// <summary>
    /// Player-side owner of a carried prop. Chairs and world items use this instead of
    /// knowing anything about the concrete player class.
    /// </summary>
    public interface IEquipmentHolder
    {
        /// <summary>Transform the carried prop is parented to. Must be non-null when equipped.</summary>
        Transform HandAnchor { get; }

        /// <summary>Currently carried prop, or null.</summary>
        ExtinguisherItem Equipped { get; }

        /// <summary>Carries the prop in hand. False when it is already held or there is no anchor.</summary>
        bool TryEquip(ExtinguisherItem extinguisher);

        /// <summary>Drops whatever is carried.</summary>
        void Unequip();
    }
}
