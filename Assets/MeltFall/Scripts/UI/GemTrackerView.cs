using System.Collections.Generic;
using MeltFall;
using UnityEngine;
using UnityEngine.UI;

namespace MeltFall.UI
{
    /// <summary>
    /// HUD gem tally (plan §6.3). Renders one slot icon per gem and reflects Pending / Landed / Lost.
    /// Binds to <see cref="LevelManager.GemsChanged"/> (landed, total) and re-reads the manager's
    /// public counters (LandedGems / LostGems / TotalGems) to colour the slots.
    ///
    /// NOTE: The runtime exposes only aggregate counts (no per-gem status event), so this view fills
    /// slots left-to-right — landed first, then lost — rather than mapping a specific gem to a specific
    /// slot. This is presentation-faithful to the visible tally but not per-instance identity. See the
    /// builder report for the missing per-gem event.
    ///
    /// Slots are built once on bind (allocation-light); per-event work only recolours existing icons.
    /// </summary>
    public sealed class GemTrackerView : MonoBehaviour
    {
        [Header("Slot construction (assign one)")]
        [Tooltip("Prefab instantiated once per gem under SlotParent. Must have an Image to tint.")]
        [SerializeField] private GameObject slotPrefab;

        [Tooltip("Parent the spawned slots are placed under (defaults to this transform).")]
        [SerializeField] private Transform slotParent;

        [Tooltip("Optional pre-authored slot images (used instead of the prefab if non-empty).")]
        [SerializeField] private Image[] prewiredSlots = new Image[0];

        [Header("State colors (serialized — no hardcoded tunables)")]
        [SerializeField] private Color pendingColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color landedColor = new Color(0.4f, 1f, 0.5f, 1f);
        [SerializeField] private Color lostColor = new Color(1f, 0.35f, 0.35f, 1f);

        private LevelManager levelManager;
        private readonly List<Image> slots = new List<Image>();
        private bool built;

        /// <summary>Wires the tracker to a level manager and builds slots for its gem count.</summary>
        public void Bind(LevelManager manager)
        {
            if (levelManager == manager)
            {
                RebuildAndRefresh();
                return;
            }

            Unhook();
            levelManager = manager;

            if (isActiveAndEnabled)
            {
                Hook();
                RebuildAndRefresh();
            }
        }

        private void OnEnable()
        {
            Hook();
            RebuildAndRefresh();
        }

        private void OnDisable()
        {
            Unhook();
        }

        private void Hook()
        {
            if (levelManager != null)
            {
                levelManager.GemsChanged += OnGemsChanged;
                levelManager.StateChanged += OnStateChanged;
            }
        }

        private void Unhook()
        {
            if (levelManager != null)
            {
                levelManager.GemsChanged -= OnGemsChanged;
                levelManager.StateChanged -= OnStateChanged;
            }
        }

        private void OnGemsChanged(int landed, int total)
        {
            EnsureSlots(total);
            RefreshColors();
        }

        // Lost gems do not raise GemsChanged in the runtime; the state transition is our other
        // opportunity to re-read the lost counter (e.g. after CollapsingResolving / Resolved).
        private void OnStateChanged(PlayState state)
        {
            RefreshColors();
        }

        private void RebuildAndRefresh()
        {
            if (levelManager != null)
            {
                EnsureSlots(levelManager.TotalGems);
            }

            RefreshColors();
        }

        private void EnsureSlots(int total)
        {
            if (prewiredSlots != null && prewiredSlots.Length > 0)
            {
                if (!built)
                {
                    slots.Clear();
                    for (int i = 0; i < prewiredSlots.Length; i++)
                    {
                        if (prewiredSlots[i] != null)
                        {
                            slots.Add(prewiredSlots[i]);
                        }
                    }

                    built = true;
                }

                // Show only the first `total` authored slots; hide the rest.
                for (int i = 0; i < slots.Count; i++)
                {
                    slots[i].gameObject.SetActive(i < total);
                }

                return;
            }

            if (slotPrefab == null)
            {
                return;
            }

            Transform parent = slotParent != null ? slotParent : transform;

            // Build once up to `total`; reuse existing on later calls.
            while (slots.Count < total)
            {
                GameObject go = Instantiate(slotPrefab, parent);
                Image img = go.GetComponent<Image>();
                if (img == null)
                {
                    img = go.GetComponentInChildren<Image>();
                }

                slots.Add(img);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].gameObject.SetActive(i < total);
                }
            }

            built = true;
        }

        private void RefreshColors()
        {
            if (levelManager == null)
            {
                return;
            }

            int landed = levelManager.LandedGems;
            int lost = levelManager.LostGems;

            for (int i = 0; i < slots.Count; i++)
            {
                Image img = slots[i];
                if (img == null || !img.gameObject.activeSelf)
                {
                    continue;
                }

                if (i < landed)
                {
                    img.color = landedColor;
                }
                else if (i < landed + lost)
                {
                    img.color = lostColor;
                }
                else
                {
                    img.color = pendingColor;
                }
            }
        }
    }
}
