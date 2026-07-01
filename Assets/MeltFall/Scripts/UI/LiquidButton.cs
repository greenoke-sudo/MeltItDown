using System;
using MeltFall;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeltFall.UI
{
    /// <summary>
    /// One selectable liquid in the bottom selector row (plan §6.4). Holds a
    /// <see cref="LiquidDefinition"/>, wraps a uGUI <see cref="Button"/>, and raises
    /// <see cref="Tapped"/> when pressed. Renders selected / idle / locked visuals via a highlight
    /// graphic. Presentation only — the owning <see cref="LiquidSelectorView"/> decides selection and
    /// drives the gun. Colors are serialized (no hardcoded tunables).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class LiquidButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;

        [Tooltip("Swatch tinted to the liquid's own color.")]
        [SerializeField] private Image swatch;

        [Tooltip("Highlight shown when this liquid is the active one.")]
        [SerializeField] private Graphic highlight;

        [Tooltip("Optional loading / purge indicator shown while this active liquid is purging.")]
        [SerializeField] private Graphic purgeIndicator;

        [SerializeField] private TMP_Text label;

        [Header("Idle / selected / locked tints (serialized — no hardcoded tunables)")]
        [SerializeField] private Color idleTint = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color selectedTint = Color.white;
        [SerializeField] private Color lockedTint = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        private LiquidDefinition liquid;
        private bool locked;

        /// <summary>Raised when this button is tapped. Arg: its liquid definition.</summary>
        public event Action<LiquidDefinition> Tapped;

        /// <summary>The liquid this button represents (null until set up).</summary>
        public LiquidDefinition Liquid => liquid;

        /// <summary>Configures this button for a liquid and its label/swatch.</summary>
        public void Setup(LiquidDefinition definition)
        {
            liquid = definition;

            if (swatch != null && liquid != null)
            {
                swatch.color = liquid.Color;
            }

            if (label != null && liquid != null)
            {
                label.SetText(liquid.DisplayName);
            }

            SetSelected(false);
            SetPurging(false);
            SetLocked(false);
        }

        private void OnEnable()
        {
            EnsureButton();
            button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }
        }

        private void EnsureButton()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void OnClicked()
        {
            if (locked || liquid == null)
            {
                return;
            }

            Tapped?.Invoke(liquid);
        }

        /// <summary>Selected (active) vs idle visual.</summary>
        public void SetSelected(bool selected)
        {
            if (highlight != null)
            {
                highlight.enabled = selected;
            }

            if (swatch != null)
            {
                swatch.color = ResolveSwatchColor(selected);
            }
        }

        /// <summary>Shows or hides the purge / loading indicator (only meaningful while selected).</summary>
        public void SetPurging(bool purging)
        {
            if (purgeIndicator != null)
            {
                purgeIndicator.enabled = purging;
            }
        }

        /// <summary>Locked (unavailable) visual; blocks taps.</summary>
        public void SetLocked(bool value)
        {
            locked = value;

            EnsureButton();
            if (button != null)
            {
                button.interactable = !locked;
            }

            if (locked && swatch != null)
            {
                swatch.color = lockedTint;
            }
        }

        private Color ResolveSwatchColor(bool selected)
        {
            if (locked)
            {
                return lockedTint;
            }

            Color baseColor = liquid != null ? liquid.Color : Color.white;
            Color tint = selected ? selectedTint : idleTint;
            return baseColor * tint;
        }
    }
}
