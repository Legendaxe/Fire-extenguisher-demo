using System;
using System.Collections.Generic;
using FireExtinguisher.Interaction;
using UnityEngine;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Routes interaction input. Discovers <see cref="IInteractionProvider"/> components on the
    /// player, exposes the highest-priority available interaction and forwards one press to it.
    /// It has no knowledge of cameras, seats or any other gameplay mode.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;

        private readonly List<IInteractionProvider> _providers = new List<IInteractionProvider>();

        public IInteractable CurrentTarget { get; private set; }

        /// <summary>Raised with the new hovered interactable, or null when nothing is available.</summary>
        public event Action<IInteractable> TargetChanged;

        private void Awake()
        {
            if (input == null)
                input = GetComponent<PlayerInputReader>();

            CollectProviders();
        }

        private void OnEnable()
        {
            if (input != null)
                input.InteractPerformed += OnInteractPerformed;
        }

        private void OnDisable()
        {
            if (input != null)
                input.InteractPerformed -= OnInteractPerformed;
        }

        private void Update()
        {
            SetTarget(ResolveTarget());
        }

        private IInteractable ResolveTarget()
        {
            for (int i = 0; i < _providers.Count; i++)
            {
                IInteractionProvider provider = _providers[i];
                if (provider == null || !provider.IsAvailable)
                    continue;

                IInteractable target = provider.Resolve(gameObject);
                if (target != null)
                    return target;
            }

            return null;
        }

        private void OnInteractPerformed()
        {
            for (int i = 0; i < _providers.Count; i++)
            {
                IInteractionProvider provider = _providers[i];
                if (provider == null || !provider.IsAvailable)
                    continue;

                if (provider.TryInteract(gameObject))
                    return;
            }
        }

        private void CollectProviders()
        {
            _providers.Clear();

            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInteractionProvider provider)
                    _providers.Add(provider);
            }

            _providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        private void SetTarget(IInteractable target)
        {
            if (ReferenceEquals(CurrentTarget, target))
                return;

            CurrentTarget = target;
            TargetChanged?.Invoke(target);
        }
    }
}
