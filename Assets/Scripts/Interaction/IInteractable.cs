using UnityEngine;

namespace FireExtinguisher.Interaction
{
    /// <summary>
    /// Anything a player can aim at and activate with the Interact input.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short verb shown in the interaction prompt, for example "Sit".</summary>
        string InteractionPrompt { get; }

        /// <summary>Whether this interactor is currently allowed to use the object.</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>Called once per activation. The interactor is the player root.</summary>
        void Interact(GameObject interactor);
    }
}
