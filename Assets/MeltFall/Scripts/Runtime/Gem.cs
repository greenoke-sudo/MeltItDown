using System;
using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// A goal item (spec §6, design §12.2). Tracks its fall distance and settle state; when it
    /// comes to rest it resolves against the level's safe/hazard zones and reports its outcome
    /// via <see cref="Resolved"/>. The <see cref="LevelManager"/> owns the tally and scoring.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Gem : MonoBehaviour
    {
        [SerializeField] private LoopTuningConfig tuning;

        private Rigidbody body;
        private GemStatus status = GemStatus.Pending;

        private float startY;
        private float maxFallDistance;
        private float settledTimer;
        private bool resolved;
        private bool initialized;

        private SafeZone[] safeZones;
        private HazardZone[] hazardZones;

        /// <summary>Resolution status of this gem.</summary>
        public GemStatus Status => status;

        /// <summary>Greatest distance this gem has fallen below its start height.</summary>
        public float MaxFallDistance => maxFallDistance;

        /// <summary>
        /// Fired once when the gem resolves (settled into a zone, or force-resolved at level end).
        /// Args: (gem, resulting status). Status is Landed or Lost.
        /// </summary>
        public event Action<Gem, GemStatus> Resolved;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            InitializeTracking();
        }

        /// <summary>
        /// Supplies the tuning config and the level's zone volumes for resolution. Called by
        /// <see cref="LevelManager"/> during bootstrap so the gem never searches the scene per-frame.
        /// </summary>
        public void Configure(LoopTuningConfig loopTuning, SafeZone[] safe, HazardZone[] hazard)
        {
            tuning = loopTuning;
            safeZones = safe;
            hazardZones = hazard;

            if (body != null && tuning != null)
            {
                body.angularDamping += tuning.GemAngularDragBoost;
            }
        }

        /// <summary>Resets fall/settle tracking to the current position (used on retry / respawn).</summary>
        public void InitializeTracking()
        {
            startY = transform.position.y;
            maxFallDistance = 0f;
            settledTimer = 0f;
            resolved = false;
            status = GemStatus.Pending;
            initialized = true;
        }

        private void FixedUpdate()
        {
            if (resolved || !initialized || body == null || tuning == null)
            {
                return;
            }

            float fallen = startY - transform.position.y;
            if (fallen > maxFallDistance)
            {
                maxFallDistance = fallen;
            }

            bool atRest = body.linearVelocity.sqrMagnitude
                              <= tuning.SettleLinearSpeedThreshold * tuning.SettleLinearSpeedThreshold
                          && body.angularVelocity.sqrMagnitude
                              <= tuning.SettleAngularSpeedThreshold * tuning.SettleAngularSpeedThreshold;

            if (atRest)
            {
                settledTimer += Time.fixedDeltaTime;
                if (settledTimer >= tuning.SettleTime)
                {
                    ResolveAtRest();
                }
            }
            else
            {
                settledTimer = 0f;
            }
        }

        private void ResolveAtRest()
        {
            if (resolved)
            {
                return;
            }

            Vector3 point = transform.position;

            // Hazard takes precedence: a gem in a kill-floor is lost regardless of fall distance.
            if (IsInHazard(point))
            {
                Finish(GemStatus.Lost);
                return;
            }

            if (maxFallDistance >= tuning.MinWinFallDistance && IsInSafe(point))
            {
                Finish(GemStatus.Landed);
                return;
            }

            // Settled somewhere that is neither a valid safe landing nor a hazard: stays Pending.
            // (LevelManager treats any still-Pending gem as lost at level end — spec §9.)
        }

        /// <summary>
        /// Forces resolution at level end. A gem that never reached a valid safe landing is lost
        /// (spec §9 wedged/never-fell handling). Safe results are preserved. No-op if already resolved.
        /// </summary>
        public void ForceResolveAtLevelEnd()
        {
            if (resolved)
            {
                return;
            }

            Vector3 point = transform.position;
            if (maxFallDistance >= (tuning != null ? tuning.MinWinFallDistance : 0f)
                && IsInSafe(point) && !IsInHazard(point))
            {
                Finish(GemStatus.Landed);
            }
            else
            {
                Finish(GemStatus.Lost);
            }
        }

        private void Finish(GemStatus result)
        {
            resolved = true;
            status = result;
            Resolved?.Invoke(this, result);
        }

        private bool IsInSafe(Vector3 point)
        {
            if (safeZones == null)
            {
                return false;
            }

            for (int i = 0; i < safeZones.Length; i++)
            {
                if (safeZones[i] != null && safeZones[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInHazard(Vector3 point)
        {
            if (hazardZones == null)
            {
                return false;
            }

            for (int i = 0; i < hazardZones.Length; i++)
            {
                if (hazardZones[i] != null && hazardZones[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
