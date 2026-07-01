using System;
using System.Collections.Generic;
using MeltFall;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeltFall.UI
{
    /// <summary>
    /// The end-of-level overlay (plan §6.6). Hidden during play. On
    /// <see cref="LevelManager.LevelCleared"/> it shows the cleared panel with the earned stars (1..3)
    /// plus Retry and Continue; on <see cref="LevelManager.LevelFailed"/> it shows a no-stars failed
    /// panel with Retry only. Retry calls <see cref="LevelManager.RetryLevel"/>; Continue raises
    /// <see cref="ContinueRequested"/> for the bootstrap/flow layer to handle (the runtime has no
    /// next-level method — see the builder report). Presentation only, no hardcoded tunables.
    /// </summary>
    public sealed class LevelResultView : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("Root of the whole overlay; disabled during play.")]
        [SerializeField] private GameObject root;

        [SerializeField] private GameObject clearedPanel;
        [SerializeField] private GameObject failedPanel;

        [Header("Cleared content")]
        [Tooltip("Star icons, index 0..2. The first `stars` are enabled.")]
        [SerializeField] private GameObject[] starIcons = new GameObject[0];

        [SerializeField] private TMP_Text clearedLabel;

        [Tooltip("Format for the cleared label. {0}=stars, {1}=gems landed, {2}=total gems.")]
        [SerializeField] private string clearedLabelFormat = "{1}/{2} gems";

        [Header("Failed content")]
        [SerializeField] private TMP_Text failedLabel;

        [Header("Buttons")]
        [SerializeField] private Button retryButtonCleared;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button retryButtonFailed;

        private LevelManager levelManager;

        /// <summary>Raised when the player taps Continue on a cleared level. Flow layer advances.</summary>
        public event Action ContinueRequested;

        /// <summary>Wires the overlay to a level manager and starts hidden.</summary>
        public void Bind(LevelManager manager)
        {
            if (levelManager == manager)
            {
                return;
            }

            Unhook();
            levelManager = manager;

            if (isActiveAndEnabled)
            {
                Hook();
            }

            HideAll();
        }

        private void OnEnable()
        {
            Hook();
            HideAll();
        }

        private void OnDisable()
        {
            Unhook();
        }

        private void Hook()
        {
            if (levelManager != null)
            {
                levelManager.LevelCleared += OnLevelCleared;
                levelManager.LevelFailed += OnLevelFailed;
            }

            AddButton(retryButtonCleared, OnRetry);
            AddButton(retryButtonFailed, OnRetry);
            AddButton(continueButton, OnContinue);
        }

        private void Unhook()
        {
            if (levelManager != null)
            {
                levelManager.LevelCleared -= OnLevelCleared;
                levelManager.LevelFailed -= OnLevelFailed;
            }

            RemoveButton(retryButtonCleared, OnRetry);
            RemoveButton(retryButtonFailed, OnRetry);
            RemoveButton(continueButton, OnContinue);
        }

        private static void AddButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private void OnLevelCleared(int stars)
        {
            ShowRoot(true);
            SetPanel(clearedPanel, true);
            SetPanel(failedPanel, false);

            int clamped = Mathf.Clamp(stars, 0, starIcons != null ? starIcons.Length : 0);
            if (starIcons != null)
            {
                for (int i = 0; i < starIcons.Length; i++)
                {
                    if (starIcons[i] != null)
                    {
                        starIcons[i].SetActive(i < clamped);
                    }
                }
            }

            if (clearedLabel != null && levelManager != null)
            {
                clearedLabel.SetText(
                    clearedLabelFormat, stars, levelManager.LandedGems, levelManager.TotalGems);
            }
        }

        private void OnLevelFailed()
        {
            ShowRoot(true);
            SetPanel(clearedPanel, false);
            SetPanel(failedPanel, true);
            // Failed panel authors its own no-stars message; failedLabel is optional.
        }

        private void OnRetry()
        {
            if (levelManager != null)
            {
                levelManager.RetryLevel();
            }

            HideAll();
        }

        private void OnContinue()
        {
            ContinueRequested?.Invoke();
            HideAll();
        }

        private void HideAll()
        {
            SetPanel(clearedPanel, false);
            SetPanel(failedPanel, false);
            ShowRoot(false);
        }

        private void ShowRoot(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private static void SetPanel(GameObject panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }
    }
}
