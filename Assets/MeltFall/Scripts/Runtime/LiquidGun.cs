using System;
using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// The liquid gun (design §12.2). Holds the active liquid, applies melt via a cone overlap
    /// cast on FixedUpdate while firing, and consumes shared fuel at the active liquid's burn rate.
    /// Switching liquid starts a purge timer that blocks firing. The overlap cast is the source of
    /// truth for melt — particles are cosmetic. Allocation-free hot path (reused overlap buffer).
    /// </summary>
    public sealed class LiquidGun : MonoBehaviour
    {
        // Local, non-tunable buffer size (not gameplay balance).
        private const int OverlapBufferSize = 32;

        // Fraction of meltPower applied per fixed tick as the raw per-tick amount base.
        // Kept as the fixed-timestep integration factor; balance lives in meltPower/burnRate SOs.

        [Header("Config")]
        [SerializeField] private LoopTuningConfig tuning;

        [Header("References")]
        [Tooltip("Origin of the melt cone (nozzle tip). Falls back to this transform if null.")]
        [SerializeField] private Transform nozzle;

        [Tooltip("Physics layers the melt cone can hit. Default: everything.")]
        [SerializeField] private LayerMask meltMask = ~0;

        private FuelTank fuelTank;
        private LiquidDefinition currentLiquid;
        private LiquidSelectorState selectorState = LiquidSelectorState.Idle;

        private bool firing;
        private float purgeTimer;
        private Vector3 aimDirection = Vector3.forward;

        private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];

        /// <summary>Currently selected liquid (may be null before selection).</summary>
        public LiquidDefinition CurrentLiquid => currentLiquid;

        /// <summary>Current selector/firing state.</summary>
        public LiquidSelectorState SelectorState => selectorState;

        /// <summary>True while a firing touch is held.</summary>
        public bool IsFiring => firing;

        /// <summary>True while the purge timer is active.</summary>
        public bool IsPurging => purgeTimer > 0f;

        /// <summary>
        /// True when the gun is allowed to fire: has a liquid, is not purging, and fuel is not empty.
        /// </summary>
        public bool CanFire =>
            currentLiquid != null && purgeTimer <= 0f && fuelTank != null && !fuelTank.IsEmpty;

        /// <summary>Fired when the active liquid changes. Arg: new liquid.</summary>
        public event Action<LiquidDefinition> LiquidSelected;

        /// <summary>Fired when the selector/firing state changes. Arg: new state.</summary>
        public event Action<LiquidSelectorState> SelectorStateChanged;

        /// <summary>Fired once when a purge completes.</summary>
        public event Action PurgeCompleted;

        /// <summary>Injects the shared fuel tank (owned by the LevelManager).</summary>
        public void SetFuelTank(FuelTank tank)
        {
            fuelTank = tank;
        }

        /// <summary>Injects the loop tuning config (cone geometry, purge delay).</summary>
        public void SetTuning(LoopTuningConfig config)
        {
            tuning = config;
        }

        /// <summary>
        /// Selects a liquid. Selecting a different liquid stops any current stream and starts a
        /// purge that blocks firing for <see cref="LoopTuningConfig.PurgeDelaySeconds"/>. Re-selecting
        /// the active liquid is a no-op (spec §9). Re-tapping while purging restarts the purge.
        /// </summary>
        public void SelectLiquid(LiquidDefinition liquid)
        {
            if (liquid == null)
            {
                return;
            }

            // Tapping the already-active, ready liquid does nothing.
            if (liquid == currentLiquid && purgeTimer <= 0f)
            {
                return;
            }

            if (firing)
            {
                EndFire();
            }

            currentLiquid = liquid;
            purgeTimer = tuning != null ? tuning.PurgeDelaySeconds : 0f;

            LiquidSelected?.Invoke(currentLiquid);
            SetSelectorState(purgeTimer > 0f ? LiquidSelectorState.Purging : LiquidSelectorState.Idle);
        }

        /// <summary>Begins firing toward the given aim ray (from <see cref="StageInputController"/>).</summary>
        public void BeginFire(Ray aimRay)
        {
            aimDirection = aimRay.direction.sqrMagnitude > 0f ? aimRay.direction.normalized : aimDirection;

            if (!CanFire)
            {
                return;
            }

            firing = true;
            SetSelectorState(LiquidSelectorState.ActiveFiring);
        }

        /// <summary>Updates the aim direction while firing (finger sweep).</summary>
        public void UpdateAim(Ray aimRay)
        {
            if (aimRay.direction.sqrMagnitude > 0f)
            {
                aimDirection = aimRay.direction.normalized;
            }
        }

        /// <summary>Stops firing (touch release).</summary>
        public void EndFire()
        {
            if (!firing)
            {
                return;
            }

            firing = false;
            SetSelectorState(purgeTimer > 0f ? LiquidSelectorState.Purging : LiquidSelectorState.Idle);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (purgeTimer > 0f)
            {
                purgeTimer -= dt;
                if (purgeTimer <= 0f)
                {
                    purgeTimer = 0f;
                    PurgeCompleted?.Invoke();
                    if (!firing)
                    {
                        SetSelectorState(LiquidSelectorState.Idle);
                    }
                }
                // No firing / no fuel drain during purge.
                return;
            }

            if (!firing || currentLiquid == null || fuelTank == null || fuelTank.IsEmpty || tuning == null)
            {
                return;
            }

            ApplyConeMelt(dt);

            // Burn shared fuel for holding the stream (even if nothing was hit).
            fuelTank.Consume(currentLiquid.BurnRatePerSecond * dt);

            if (fuelTank.IsEmpty)
            {
                EndFire();
            }
        }

        private void ApplyConeMelt(float dt)
        {
            Transform origin = nozzle != null ? nozzle : transform;
            float reach = tuning.ConeReach;
            Vector3 originPos = origin.position;

            // Sample a sphere at the cone's far region, then filter by cone half-angle.
            Vector3 sphereCenter = originPos + aimDirection * (reach * 0.5f);
            float sphereRadius = reach * 0.5f;

            int hitCount = Physics.OverlapSphereNonAlloc(
                sphereCenter, sphereRadius, overlapBuffer, meltMask, QueryTriggerInteraction.Ignore);

            float cosHalfAngle = Mathf.Cos(tuning.ConeHalfAngleDegrees * Mathf.Deg2Rad);
            float meltPerTick = currentLiquid.MeltPower * dt;
            float reachSqr = reach * reach;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = overlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                MeltableMaterial piece = col.GetComponentInParent<MeltableMaterial>();
                if (piece == null || piece.IsCleared)
                {
                    continue;
                }

                Vector3 toTarget = piece.transform.position - originPos;
                float distSqr = toTarget.sqrMagnitude;
                if (distSqr > reachSqr || distSqr <= 0f)
                {
                    continue;
                }

                Vector3 dir = toTarget / Mathf.Sqrt(distSqr);
                if (Vector3.Dot(dir, aimDirection) < cosHalfAngle)
                {
                    continue;
                }

                // ApplyMelt handles matched vs wrong-liquid internally.
                piece.ApplyMelt(currentLiquid, meltPerTick);
            }
        }

        private void SetSelectorState(LiquidSelectorState newState)
        {
            if (selectorState == newState)
            {
                return;
            }

            selectorState = newState;
            SelectorStateChanged?.Invoke(selectorState);
        }
    }
}
