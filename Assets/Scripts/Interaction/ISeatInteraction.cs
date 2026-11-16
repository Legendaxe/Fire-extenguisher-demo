using UnityEngine;

namespace FireExtinguisher.Interaction
{
    /// <summary>
    /// A seat-owned interaction that is offered while the player is sitting on that seat,
    /// for example picking up a fire extinguisher that is lying next to the chair.
    /// </summary>
    public interface ISeatInteraction : IInteractable
    {
    }
}
