using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireExtinguisher.Player
{
    /// <summary>
    /// Single source of player input. Other components read values from here instead of
    /// binding to the Input System themselves, so one asset drives the whole player.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Actions Asset")]
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string actionMapName = "Player";

        [Header("Action Names")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string sprintActionName = "Sprint";
        [SerializeField] private string interactActionName = "Interact";
        [Tooltip("Existing 'Attack' action, used as the rocket fire button (LMB / gamepad West).")]
        [SerializeField] private string fireActionName = "Attack";
        [Tooltip("Action that throws the carried extinguisher back into the world (G / gamepad East).")]
        [SerializeField] private string dropActionName = "Drop";

        private InputActionMap _map;
        private InputAction _move;
        private InputAction _look;
        private InputAction _jump;
        private InputAction _sprint;
        private InputAction _interact;
        private InputAction _fire;
        private InputAction _drop;

        /// <summary>
        /// Debug and test override for the fire button. Null samples the real Attack action.
        /// The headless smoke test uses this because a batchmode editor has no focused Game view.
        /// </summary>
        public bool? FireOverride { get; set; }

        /// <summary>Debug and test override for the drop button. Null samples the real Drop action.</summary>
        public bool? DropOverride { get; set; }

        /// <summary>Raised once per Interact press.</summary>
        public event Action InteractPerformed;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintHeld => _sprint != null && _sprint.IsPressed();
        public bool JumpWasPressed => _jump != null && _jump.WasPressedThisFrame();
        public bool FireHeld => FireOverride ?? (_fire != null && _fire.IsPressed());
        public bool DropWasPressed => DropOverride ?? (_drop != null && _drop.WasPressedThisFrame());

        /// <summary>True when the input asset actually contains the Drop action.</summary>
        public bool HasDropAction => _drop != null;

        /// <summary>True when the last look value came from a mouse/pen rather than a stick.</summary>
        public bool LookFromPointer =>
            _look != null &&
            _look.activeControl != null &&
            _look.activeControl.device is Pointer;

        private void Awake()
        {
            ResolveActions();
        }

        private void OnEnable()
        {
            if (_map == null)
                ResolveActions();

            _map?.Enable();

            if (_interact != null)
                _interact.performed += HandleInteract;
        }

        private void OnDisable()
        {
            if (_interact != null)
                _interact.performed -= HandleInteract;

            _map?.Disable();
        }

        private void ResolveActions()
        {
            InputActionAsset asset = actions != null ? actions : InputSystem.actions;
            if (asset == null)
            {
                Debug.LogWarning($"{nameof(PlayerInputReader)}: no Input Actions asset assigned.", this);
                return;
            }

            _map = asset.FindActionMap(actionMapName, false);
            if (_map == null)
            {
                Debug.LogWarning($"{nameof(PlayerInputReader)}: action map '{actionMapName}' not found.", this);
                return;
            }

            _move = _map.FindAction(moveActionName, false);
            _look = _map.FindAction(lookActionName, false);
            _jump = _map.FindAction(jumpActionName, false);
            _sprint = _map.FindAction(sprintActionName, false);
            _interact = _map.FindAction(interactActionName, false);
            _fire = _map.FindAction(fireActionName, false);
            _drop = _map.FindAction(dropActionName, false);
        }

        private void HandleInteract(InputAction.CallbackContext context)
        {
            InteractPerformed?.Invoke();
        }
    }
}
