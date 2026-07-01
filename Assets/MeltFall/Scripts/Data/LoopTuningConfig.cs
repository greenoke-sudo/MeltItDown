using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// Shared loop timing + feel tunables (design §12.1, plan §5). Every gameplay-feel
    /// number lives here so nothing is baked into runtime code.
    /// </summary>
    [CreateAssetMenu(fileName = "LoopTuning", menuName = "MeltFall/Loop Tuning Config")]
    public sealed class LoopTuningConfig : ScriptableObject
    {
        [Header("Liquid swap")]
        [Tooltip("Seconds the purge lasts after switching liquid; firing is blocked during this window.")]
        [SerializeField] private float purgeDelaySeconds = 0.5f;

        [Header("Fuel")]
        [Tooltip("Fraction (0..1) of the tank at/below which the gauge is considered low.")]
        [Range(0f, 1f)]
        [SerializeField] private float lowFuelThreshold01 = 0.25f;

        [Header("Win guard")]
        [Tooltip("Least distance a gem must fall (world units) to count as a valid landing.")]
        [SerializeField] private float minWinFallDistance = 1.5f;

        [Header("Settle detection")]
        [Tooltip("Linear speed below which a body is considered at rest.")]
        [SerializeField] private float settleLinearSpeedThreshold = 0.05f;

        [Tooltip("Angular speed (rad/s) below which a body is considered at rest.")]
        [SerializeField] private float settleAngularSpeedThreshold = 0.05f;

        [Tooltip("Continuous seconds under the speed thresholds before a body is declared settled.")]
        [SerializeField] private float settleTime = 0.5f;

        [Header("Melt cone")]
        [Tooltip("Half-angle of the melt cone in degrees.")]
        [SerializeField] private float coneHalfAngleDegrees = 12f;

        [Tooltip("Maximum reach of the melt cone (world units).")]
        [SerializeField] private float coneReach = 6f;

        [Header("Gem fall guidance")]
        [Tooltip("Additional angular drag applied to gems to bias clean, readable falls.")]
        [SerializeField] private float gemAngularDragBoost = 2f;

        [Header("Camera")]
        [Tooltip("Downward 3/4 tilt of the fixed stage camera (degrees).")]
        [SerializeField] private float cameraTiltDegrees = 35f;

        [Tooltip("Maximum parallax pan offset (world units) from the pointer.")]
        [SerializeField] private float parallaxMax = 0.75f;

        [Header("Landing emphasis")]
        [Tooltip("Time scale used during the landing slow-mo pinch.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float landingSlowMoScale = 0.35f;

        [Tooltip("Real-time seconds the landing slow-mo lasts.")]
        [SerializeField] private float landingSlowMoSeconds = 0.4f;

        public float PurgeDelaySeconds => purgeDelaySeconds;
        public float LowFuelThreshold01 => lowFuelThreshold01;
        public float MinWinFallDistance => minWinFallDistance;
        public float SettleLinearSpeedThreshold => settleLinearSpeedThreshold;
        public float SettleAngularSpeedThreshold => settleAngularSpeedThreshold;
        public float SettleTime => settleTime;
        public float ConeHalfAngleDegrees => coneHalfAngleDegrees;
        public float ConeReach => coneReach;
        public float GemAngularDragBoost => gemAngularDragBoost;
        public float CameraTiltDegrees => cameraTiltDegrees;
        public float ParallaxMax => parallaxMax;
        public float LandingSlowMoScale => landingSlowMoScale;
        public float LandingSlowMoSeconds => landingSlowMoSeconds;
    }
}
