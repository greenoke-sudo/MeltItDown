using MeltFall.UI;
using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// Runtime wiring glue between the level systems and the instantiated HUD prefab. The HUD views
    /// only bind at runtime (they take live references — a <see cref="FuelTank"/>, a
    /// <see cref="LevelManager"/>, a <see cref="LiquidGun"/> — that cannot be serialized from the
    /// prefab), so this component finds those systems in the scene after the
    /// <see cref="LevelManager"/> has bootstrapped and calls each view's Bind method in the right
    /// order. It also drives the <see cref="AimIndicatorView"/> each frame from the
    /// <see cref="StageInputController"/>, since no other runtime script owns that marker.
    ///
    /// References may be assigned by the scene builder; any left null are auto-found on Start. No
    /// hardcoded tunables — this is pure plumbing.
    /// </summary>
    public sealed class SceneBootstrap : MonoBehaviour
    {
        [Header("Systems (auto-found if left empty)")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private LiquidGun liquidGun;
        [SerializeField] private StageInputController inputController;

        [Header("HUD views (auto-found if left empty)")]
        [SerializeField] private FuelGaugeView fuelGauge;
        [SerializeField] private GemTrackerView gemTracker;
        [SerializeField] private LiquidSelectorView liquidSelector;
        [SerializeField] private AimIndicatorView aimIndicator;
        [SerializeField] private LevelResultView levelResult;

        private bool aimShowing;

        // Start (not Awake) so every LevelManager/LiquidGun Awake + LevelManager.Bootstrap has run
        // first — Unity guarantees all Awake calls precede any Start call.
        private void Start()
        {
            Resolve();
            BindViews();
        }

        private void Resolve()
        {
            if (levelManager == null)
            {
                levelManager = FindFirstObjectByType<LevelManager>();
            }

            if (liquidGun == null)
            {
                liquidGun = FindFirstObjectByType<LiquidGun>();
            }

            if (inputController == null)
            {
                inputController = FindFirstObjectByType<StageInputController>();
            }

            if (fuelGauge == null)
            {
                fuelGauge = FindFirstObjectByType<FuelGaugeView>(FindObjectsInactive.Include);
            }

            if (gemTracker == null)
            {
                gemTracker = FindFirstObjectByType<GemTrackerView>(FindObjectsInactive.Include);
            }

            if (liquidSelector == null)
            {
                liquidSelector = FindFirstObjectByType<LiquidSelectorView>(FindObjectsInactive.Include);
            }

            if (aimIndicator == null)
            {
                aimIndicator = FindFirstObjectByType<AimIndicatorView>(FindObjectsInactive.Include);
            }

            if (levelResult == null)
            {
                levelResult = FindFirstObjectByType<LevelResultView>(FindObjectsInactive.Include);
            }
        }

        private void BindViews()
        {
            if (fuelGauge != null && levelManager != null)
            {
                fuelGauge.Bind(levelManager.Fuel);
                fuelGauge.BindTuning(levelManager.ActiveTuning);
            }

            if (gemTracker != null && levelManager != null)
            {
                gemTracker.Bind(levelManager);
            }

            if (liquidSelector != null && liquidGun != null)
            {
                // The selector resolves buttons from the gun's actual liquid list (already the level's
                // available set, wired by the scene builder), avoiding an id->definition registry.
                liquidSelector.Bind(liquidGun, liquidGun.AvailableLiquids);
            }

            if (levelResult != null && levelManager != null)
            {
                levelResult.Bind(levelManager);
            }
        }

        private void Update()
        {
            if (aimIndicator == null || inputController == null)
            {
                return;
            }

            if (inputController.IsFiring)
            {
                aimIndicator.Show(inputController.CurrentAimPoint);
                aimShowing = true;
            }
            else if (aimShowing)
            {
                aimIndicator.Hide();
                aimShowing = false;
            }
        }
    }
}
