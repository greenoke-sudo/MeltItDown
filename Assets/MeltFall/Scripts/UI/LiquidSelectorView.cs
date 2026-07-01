using System.Collections.Generic;
using MeltFall;
using UnityEngine;

namespace MeltFall.UI
{
    /// <summary>
    /// The bottom selector row (plan §6.4). Builds one <see cref="LiquidButton"/> per available liquid,
    /// tracks the active liquid, reflects the gun's <see cref="LiquidSelectorState"/> (Idle / Purging /
    /// ActiveFiring), and calls <see cref="LiquidGun.SelectLiquid"/> on tap. Presentation only — the
    /// gun owns the actual selection/purge logic; this view just mirrors it and forwards taps.
    ///
    /// Buttons are built once on bind (allocation-light). No hardcoded tunables here; per-button colors
    /// live on <see cref="LiquidButton"/>.
    /// </summary>
    public sealed class LiquidSelectorView : MonoBehaviour
    {
        [Header("Button construction")]
        [Tooltip("Prefab with a LiquidButton, instantiated once per available liquid.")]
        [SerializeField] private LiquidButton buttonPrefab;

        [Tooltip("Row parent for spawned buttons (defaults to this transform).")]
        [SerializeField] private Transform buttonParent;

        private LiquidGun gun;
        private readonly List<LiquidButton> buttons = new List<LiquidButton>();
        private LiquidDefinition activeLiquid;
        private bool built;

        /// <summary>
        /// Wires the selector to the gun and builds a button per available liquid. Call from the
        /// bootstrap once the level's liquids are resolved from ids to definitions.
        /// </summary>
        public void Bind(LiquidGun liquidGun, IReadOnlyList<LiquidDefinition> availableLiquids)
        {
            Unhook();
            gun = liquidGun;

            BuildButtons(availableLiquids);

            if (isActiveAndEnabled)
            {
                Hook();
            }

            activeLiquid = gun != null ? gun.CurrentLiquid : null;
            RefreshSelection();
            if (gun != null)
            {
                ApplyState(gun.SelectorState);
            }
        }

        private void OnEnable()
        {
            Hook();
            if (gun != null)
            {
                activeLiquid = gun.CurrentLiquid;
                RefreshSelection();
                ApplyState(gun.SelectorState);
            }
        }

        private void OnDisable()
        {
            Unhook();
        }

        private void Hook()
        {
            if (gun != null)
            {
                gun.LiquidSelected += OnLiquidSelected;
                gun.SelectorStateChanged += OnSelectorStateChanged;
                gun.PurgeCompleted += OnPurgeCompleted;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Tapped += OnButtonTapped;
                }
            }
        }

        private void Unhook()
        {
            if (gun != null)
            {
                gun.LiquidSelected -= OnLiquidSelected;
                gun.SelectorStateChanged -= OnSelectorStateChanged;
                gun.PurgeCompleted -= OnPurgeCompleted;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Tapped -= OnButtonTapped;
                }
            }
        }

        private void BuildButtons(IReadOnlyList<LiquidDefinition> availableLiquids)
        {
            if (buttonPrefab == null || availableLiquids == null)
            {
                return;
            }

            Transform parent = buttonParent != null ? buttonParent : transform;

            // Detach existing tap handlers before we (re)build so we don't double-subscribe.
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Tapped -= OnButtonTapped;
                }
            }

            // Grow / reuse the pool to match the liquid count.
            while (buttons.Count < availableLiquids.Count)
            {
                LiquidButton created = Instantiate(buttonPrefab, parent);
                buttons.Add(created);
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                LiquidButton b = buttons[i];
                if (b == null)
                {
                    continue;
                }

                if (i < availableLiquids.Count)
                {
                    b.gameObject.SetActive(true);
                    b.Setup(availableLiquids[i]);
                }
                else
                {
                    b.gameObject.SetActive(false);
                }
            }

            built = true;
        }

        private void OnButtonTapped(LiquidDefinition liquid)
        {
            if (gun != null)
            {
                gun.SelectLiquid(liquid);
            }
        }

        private void OnLiquidSelected(LiquidDefinition liquid)
        {
            activeLiquid = liquid;
            RefreshSelection();
        }

        private void OnSelectorStateChanged(LiquidSelectorState state)
        {
            ApplyState(state);
        }

        private void OnPurgeCompleted()
        {
            // Purge finished: clear the loading visual on the active button.
            ApplyState(gun != null ? gun.SelectorState : LiquidSelectorState.Idle);
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                LiquidButton b = buttons[i];
                if (b == null)
                {
                    continue;
                }

                b.SetSelected(b.Liquid != null && b.Liquid == activeLiquid);
            }
        }

        private void ApplyState(LiquidSelectorState state)
        {
            bool purging = state == LiquidSelectorState.Purging;

            for (int i = 0; i < buttons.Count; i++)
            {
                LiquidButton b = buttons[i];
                if (b == null)
                {
                    continue;
                }

                bool isActive = b.Liquid != null && b.Liquid == activeLiquid;
                // Loading indicator only on the active liquid while purging.
                b.SetPurging(isActive && purging);
            }
        }

        /// <summary>Whether the button pool has been constructed at least once.</summary>
        public bool IsBuilt => built;
    }
}
