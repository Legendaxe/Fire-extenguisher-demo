using FireExtinguisher.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FireExtinguisher.UI
{
    /// <summary>
    /// Simple fuel readout for the carried extinguisher. Hidden while the player is empty-handed,
    /// turns from green to red as the tank empties, and shows a percentage.
    /// </summary>
    [DisallowMultipleComponent]
    public class FuelGaugeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private GameObject gaugeRoot;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text label;

        [Header("Look")]
        [SerializeField] private Color fullColor = new Color(0.35f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.95f, 0.25f, 0.15f, 1f);
        [SerializeField] private string format = "FUEL {0:0}%";

        private void Awake()
        {
            if (equipment == null)
                equipment = FindFirstObjectByType<PlayerEquipment>();
        }

        private void LateUpdate()
        {
            if (gaugeRoot == null)
                return;

            bool show = equipment != null && equipment.Equipped != null;

            if (gaugeRoot.activeSelf != show)
                gaugeRoot.SetActive(show);

            if (!show)
                return;

            float fuel = equipment.Equipped.Fuel01;

            if (fillImage != null)
            {
                fillImage.fillAmount = fuel;
                fillImage.color = Color.Lerp(emptyColor, fullColor, fuel);
            }

            if (label != null)
                label.text = string.Format(format, fuel * 100f);
        }
    }
}
