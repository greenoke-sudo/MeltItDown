using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// The play-state machine and level arbiter (design §12.2, plan §4/§6). Owns the shared
    /// <see cref="FuelTank"/> and <see cref="PlayState"/>, tracks gem resolution, computes stars,
    /// and drives retry. Exposes C# events for a HUD (built later) to subscribe to — this layer
    /// references no UI classes. All tunables come from the assigned SO configs; nothing hardcoded.
    /// </summary>
    public sealed class LevelManager : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private LevelDefinition levelDefinition;
        [SerializeField] private LoopTuningConfig loopTuning;

        [Header("Fuel (owned)")]
        [SerializeField] private FuelTank fuelTank = new FuelTank();

        [Header("Scene references")]
        [SerializeField] private LiquidGun liquidGun;
        [SerializeField] private StageCameraRig cameraRig;

        [Tooltip("Gems in the level. Auto-found in the scene on bootstrap if left empty.")]
        [SerializeField] private List<Gem> gems = new List<Gem>();

        [Tooltip("Safe landing zones. Auto-found in the scene on bootstrap if left empty.")]
        [SerializeField] private List<SafeZone> safeZones = new List<SafeZone>();

        [Tooltip("Hazard kill-floors. Auto-found in the scene on bootstrap if left empty.")]
        [SerializeField] private List<HazardZone> hazardZones = new List<HazardZone>();

        private PlayState state = PlayState.Surveying;
        private int totalGems;
        private int landedGems;
        private int lostGems;
        private bool resolved;

        private SafeZone[] safeZoneArray;
        private HazardZone[] hazardZoneArray;

        private Coroutine landingBeatRoutine;
        private bool appPaused;

        /// <summary>The active loop tuning (level override if present, else the manager's).</summary>
        public LoopTuningConfig ActiveTuning =>
            levelDefinition != null && levelDefinition.Tuning != null ? levelDefinition.Tuning : loopTuning;

        /// <summary>Current top-level play state.</summary>
        public PlayState State => state;

        /// <summary>True while the app is backgrounded / has lost focus (spec §9).</summary>
        public bool IsAppPaused => appPaused;

        /// <summary>The shared fuel tank.</summary>
        public FuelTank Fuel => fuelTank;

        /// <summary>Total gems in the level.</summary>
        public int TotalGems => totalGems;

        /// <summary>Gems that landed safely.</summary>
        public int LandedGems => landedGems;

        /// <summary>Gems that were lost.</summary>
        public int LostGems => lostGems;

        /// <summary>Fired whenever the play state changes.</summary>
        public event Action<PlayState> StateChanged;

        /// <summary>Fired when the gem tally changes. Args: (landed, total).</summary>
        public event Action<int, int> GemsChanged;

        /// <summary>Fired when the level clears (>= 1 gem landed). Arg: stars (1..3).</summary>
        public event Action<int> LevelCleared;

        /// <summary>Fired when the level fails (fuel empty with zero gems landed).</summary>
        public event Action LevelFailed;

        private void Awake()
        {
            Bootstrap();
        }

        private void OnDisable()
        {
            Unsubscribe();

            // Never leave the game frozen if we are disabled mid-beat.
            StopLandingBeat();
        }

        /// <summary>
        /// App backgrounded/foregrounded (spec §9 Interruptions). Stops the stream on pause so fuel
        /// can't drain while unattended; clears the paused flag on resume. Does not touch timeScale.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            HandleInterruption(paused);
        }

        /// <summary>
        /// Focus lost/gained (editor/desktop parity with <see cref="OnApplicationPause"/>). Treated
        /// the same way: stop the stream when focus is lost.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            HandleInterruption(!hasFocus);
        }

        private void HandleInterruption(bool interrupted)
        {
            appPaused = interrupted;

            if (interrupted && liquidGun != null)
            {
                liquidGun.EndFire();
            }
        }

        /// <summary>
        /// Wires the level: fuel from the LevelDefinition, tuning into the gun/camera/gems, and
        /// gem/zone discovery + subscription. Safe to call again (retry re-uses it via reset).
        /// </summary>
        private void Bootstrap()
        {
            if (levelDefinition != null)
            {
                fuelTank.Initialize(levelDefinition.StartingFuel);
            }

            fuelTank.Emptied -= OnFuelEmptied;
            fuelTank.Emptied += OnFuelEmptied;

            if (gems.Count == 0)
            {
                gems.AddRange(FindObjectsByType<Gem>(FindObjectsSortMode.None));
            }

            if (safeZones.Count == 0)
            {
                safeZones.AddRange(FindObjectsByType<SafeZone>(FindObjectsSortMode.None));
            }

            if (hazardZones.Count == 0)
            {
                hazardZones.AddRange(FindObjectsByType<HazardZone>(FindObjectsSortMode.None));
            }

            safeZoneArray = safeZones.ToArray();
            hazardZoneArray = hazardZones.ToArray();

            LoopTuningConfig tuning = ActiveTuning;
            if (liquidGun != null)
            {
                liquidGun.SetFuelTank(fuelTank);
                liquidGun.SetTuning(tuning);
                liquidGun.SetFiringBlocked(false);

                // Mirror the gun's selector state into the play state (event-driven, no per-frame work).
                liquidGun.SelectorStateChanged -= OnSelectorStateChanged;
                liquidGun.SelectorStateChanged += OnSelectorStateChanged;
            }

            // React to any support piece clearing (spec §7): collapse begins.
            MeltableMaterial.Cleared -= OnMeltableCleared;
            MeltableMaterial.Cleared += OnMeltableCleared;

            if (cameraRig != null)
            {
                cameraRig.SetTuning(tuning);
            }

            totalGems = gems.Count;
            landedGems = 0;
            lostGems = 0;
            resolved = false;

            for (int i = 0; i < gems.Count; i++)
            {
                Gem gem = gems[i];
                if (gem == null)
                {
                    continue;
                }

                gem.Configure(tuning, safeZoneArray, hazardZoneArray);
                gem.Resolved -= OnGemResolved;
                gem.Resolved += OnGemResolved;
            }

            SetState(PlayState.Surveying);
            GemsChanged?.Invoke(landedGems, totalGems);
        }

        private void Unsubscribe()
        {
            fuelTank.Emptied -= OnFuelEmptied;

            if (liquidGun != null)
            {
                liquidGun.SelectorStateChanged -= OnSelectorStateChanged;
            }

            MeltableMaterial.Cleared -= OnMeltableCleared;

            for (int i = 0; i < gems.Count; i++)
            {
                if (gems[i] != null)
                {
                    gems[i].Resolved -= OnGemResolved;
                }
            }
        }

        /// <summary>Transitions the play state and notifies listeners.</summary>
        public void SetState(PlayState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
            StateChanged?.Invoke(state);
        }

        /// <summary>
        /// Mirrors the gun's selector state into the play state while the level is live. Only maps
        /// while in one of {Surveying, Spraying, PurgeDelay} so it never overrides
        /// <see cref="PlayState.CollapsingResolving"/> or <see cref="PlayState.Resolved"/>.
        /// </summary>
        private void OnSelectorStateChanged(LiquidSelectorState selectorState)
        {
            if (state != PlayState.Surveying
                && state != PlayState.Spraying
                && state != PlayState.PurgeDelay)
            {
                return;
            }

            switch (selectorState)
            {
                case LiquidSelectorState.ActiveFiring:
                    SetState(PlayState.Spraying);
                    break;
                case LiquidSelectorState.Purging:
                    SetState(PlayState.PurgeDelay);
                    break;
                case LiquidSelectorState.Idle:
                default:
                    SetState(PlayState.Surveying);
                    break;
            }
        }

        /// <summary>
        /// A support piece cleared (spec §7): unless already resolved, the level enters the
        /// collapse/resolve phase so falling bodies can settle.
        /// </summary>
        private void OnMeltableCleared(MeltableMaterial piece)
        {
            if (state == PlayState.Resolved)
            {
                return;
            }

            SetState(PlayState.CollapsingResolving);
        }

        private void OnGemResolved(Gem gem, GemStatus status)
        {
            if (status == GemStatus.Landed)
            {
                MarkLanded(gem);
            }
            else
            {
                MarkLost(gem);
            }
        }

        /// <summary>Records a safe landing, plays the landing emphasis beat, and re-checks resolution.</summary>
        public void MarkLanded(Gem gem)
        {
            landedGems++;
            GemsChanged?.Invoke(landedGems, totalGems);
            PlayLandingBeat();
            CheckResolution();
        }

        /// <summary>Records a lost gem and re-checks resolution.</summary>
        public void MarkLost(Gem gem)
        {
            lostGems++;
            GemsChanged?.Invoke(landedGems, totalGems);
            CheckResolution();
        }

        /// <summary>
        /// Triggers a brief slow-mo emphasis on a safe landing (spec §6.4). No-op if there is no
        /// active tuning. Re-entrancy safe: an in-flight beat is restarted so timeScale is always
        /// driven by a single running coroutine.
        /// </summary>
        private void PlayLandingBeat()
        {
            LoopTuningConfig tuning = ActiveTuning;
            if (tuning == null)
            {
                return;
            }

            if (landingBeatRoutine != null)
            {
                StopCoroutine(landingBeatRoutine);
                landingBeatRoutine = null;
            }

            landingBeatRoutine = StartCoroutine(LandingBeat(tuning.LandingSlowMoScale, tuning.LandingSlowMoSeconds));
        }

        private IEnumerator LandingBeat(float scale, float seconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            landingBeatRoutine = null;
        }

        private void OnFuelEmptied()
        {
            if (liquidGun != null)
            {
                liquidGun.EndFire();
            }

            // Fuel out: allow in-flight bodies to settle, then force-resolve any pending gems.
            SetState(PlayState.CollapsingResolving);
            ForceResolvePendingGems();
        }

        private void ForceResolvePendingGems()
        {
            for (int i = 0; i < gems.Count; i++)
            {
                Gem gem = gems[i];
                if (gem != null && gem.Status == GemStatus.Pending)
                {
                    gem.ForceResolveAtLevelEnd();
                }
            }

            CheckResolution();
        }

        private void CheckResolution()
        {
            if (resolved)
            {
                return;
            }

            bool allGemsResolved = (landedGems + lostGems) >= totalGems && totalGems > 0;
            bool fuelDry = fuelTank.IsEmpty;

            if (allGemsResolved || fuelDry)
            {
                Resolve();
            }
        }

        /// <summary>
        /// Finalizes the level: enters <see cref="PlayState.Resolved"/> and fires the cleared
        /// (with stars) or failed event. Idempotent.
        /// </summary>
        public void Resolve()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;
            SetState(PlayState.Resolved);

            // Block all further firing once the level is resolved (spec §6.5, §8).
            if (liquidGun != null)
            {
                liquidGun.SetFiringBlocked(true);
            }

            if (landedGems >= 1)
            {
                int stars = levelDefinition != null
                    ? Mathf.Clamp(levelDefinition.StarsForLanded(landedGems), 1, 3)
                    : Mathf.Clamp(landedGems, 1, 3);
                LevelCleared?.Invoke(stars);
            }
            else
            {
                LevelFailed?.Invoke();
            }
        }

        /// <summary>
        /// Resets the level to its authored start: refuels, clears the tally, re-centers the
        /// camera, and re-initializes gem tracking. Object respawn (destroy/re-instantiate) is the
        /// caller's responsibility (e.g. scene reload); this clears the logical state cleanly.
        /// </summary>
        public void RetryLevel()
        {
            fuelTank.ResetToStart();

            landedGems = 0;
            lostGems = 0;
            resolved = false;

            // Never leave a slow-mo beat stuck across a retry.
            StopLandingBeat();

            if (liquidGun != null)
            {
                liquidGun.SetFiringBlocked(false);
            }

            for (int i = 0; i < gems.Count; i++)
            {
                if (gems[i] != null)
                {
                    gems[i].InitializeTracking();
                }
            }

            if (cameraRig != null)
            {
                cameraRig.ResetView();
            }

            SetState(PlayState.Surveying);
            GemsChanged?.Invoke(landedGems, totalGems);
        }

        /// <summary>Stops any running landing beat and restores normal time flow.</summary>
        private void StopLandingBeat()
        {
            if (landingBeatRoutine != null)
            {
                StopCoroutine(landingBeatRoutine);
                landingBeatRoutine = null;
            }

            Time.timeScale = 1f;
        }
    }
}
