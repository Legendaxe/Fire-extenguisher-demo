using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Default aiming: raycasts from the view camera and returns the first interactable that
    /// accepts this player. Mode providers with a higher priority shadow it.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaycastInteractionProvider : MonoBehaviour, IInteractionProvider
    {
        [Header("Aiming")]
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Ordering")]
        [Tooltip("Lower than mode providers, so sitting or driving can shadow normal aiming.")]
        [SerializeField] private int priority;

        public int Priority => priority;
        public bool IsAvailable => true;

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
        }

        public IInteractable Resolve(GameObject interactor)
        {
            if (interactor == null)
                return null;

            Camera camera = ResolveCamera();
            if (camera == null)
                return null;

            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, triggerInteraction))
                return null;

            // Ignore our own colliders. Do not use transform.root here: the player may be
            // parented under a scene group, which would make every object look like "self".
            if (hit.collider.transform.IsChildOf(interactor.transform))
                return null;

            IInteractable candidate = hit.collider.GetComponentInParent<IInteractable>();
            return candidate != null && candidate.CanInteract(interactor) ? candidate : null;
        }

        public bool TryInteract(GameObject interactor)
        {
            IInteractable target = Resolve(interactor);
            if (target == null)
                return false;

            target.Interact(interactor);
            return true;
        }

        private Camera ResolveCamera()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera == null)
                viewCamera = Camera.main;

            return viewCamera;
        }
    }
}
