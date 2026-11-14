using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// A source of usable interactions for one player. Higher priority providers win, so a
    /// temporary mode (sitting, driving, carrying) can shadow normal aiming without editing
    /// <see cref="PlayerInteractor"/>.
    /// </summary>
    public interface IInteractionProvider
    {
        /// <summary>Higher wins. Aiming uses a low value; modes use higher ones.</summary>
        int Priority { get; }

        /// <summary>False while the provider cannot supply interactions (for example, not seated).</summary>
        bool IsAvailable { get; }

        /// <summary>The interaction to show and use right now, or null for none.</summary>
        IInteractable Resolve(GameObject interactor);

        /// <summary>Handles one Interact press. True when the press was consumed.</summary>
        bool TryInteract(GameObject interactor);
    }
}
