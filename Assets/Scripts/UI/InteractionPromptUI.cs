using FireExtinguisher.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FireExtinguisher.UI
{
    /// <summary>
    /// Bottom-of-screen prompt. Shows whatever the interactor currently resolves, which is the
    /// hovered object's verb while exploring and the seat's stand prompt while seated.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private GameObject promptRoot;
        [SerializeField] private Text promptLabel;

        [Header("Copy")]
        [SerializeField] private string inputHint = "E / Gamepad Y";

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (promptRoot == null || promptLabel == null)
                return;

            string verb = ResolvePrompt();
            bool show = !string.IsNullOrEmpty(verb);

            if (promptRoot.activeSelf != show)
                promptRoot.SetActive(show);

            if (show)
                promptLabel.text = $"{verb} ({inputHint})";
        }

        private string ResolvePrompt()
        {
            return interactor != null && interactor.CurrentTarget != null
                ? interactor.CurrentTarget.InteractionPrompt
                : null;
        }
    }
}
