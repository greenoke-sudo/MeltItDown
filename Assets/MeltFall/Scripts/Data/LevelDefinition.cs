using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// Per-level numeric tunables (design §12.1, plan §5). Data-only: gem spawns and
    /// safe/hazard zone volumes are placed in-scene; this asset holds the balance numbers.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "MeltFall/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Header("Economy")]
        [Tooltip("Hard fuel budget for the shared tank; no mid-level refill.")]
        [SerializeField] private float startingFuel = 100f;

        [Tooltip("Ids of the liquids available in this level's selector.")]
        [SerializeField] private string[] availableLiquidIds = new string[0];

        [Header("Gems")]
        [Tooltip("Number of goal gems in this level (1..3). Spawn transforms are placed in-scene.")]
        [SerializeField] private int gemCount = 1;

        [Header("Star thresholds (gems landed -> stars)")]
        [Tooltip("Gems landed required for 1 star.")]
        [SerializeField] private int starThreshold1 = 1;

        [Tooltip("Gems landed required for 2 stars.")]
        [SerializeField] private int starThreshold2 = 2;

        [Tooltip("Gems landed required for 3 stars.")]
        [SerializeField] private int starThreshold3 = 3;

        [Header("Tuning")]
        [Tooltip("Optional shared loop tuning override for this level. If null, the manager's tuning is used.")]
        [SerializeField] private LoopTuningConfig tuning;

        /// <summary>Hard starting fuel budget.</summary>
        public float StartingFuel => startingFuel;

        /// <summary>Ids of the liquids available in this level.</summary>
        public string[] AvailableLiquidIds => availableLiquidIds;

        /// <summary>Number of goal gems (spawns placed in-scene).</summary>
        public int GemCount => gemCount;

        /// <summary>Gems landed required for 1 star.</summary>
        public int StarThreshold1 => starThreshold1;

        /// <summary>Gems landed required for 2 stars.</summary>
        public int StarThreshold2 => starThreshold2;

        /// <summary>Gems landed required for 3 stars.</summary>
        public int StarThreshold3 => starThreshold3;

        /// <summary>Optional per-level tuning override (may be null).</summary>
        public LoopTuningConfig Tuning => tuning;

        /// <summary>
        /// Maps a landed-gem count to a star rating (0..3), clamped through the thresholds.
        /// A cleared level always yields at least 1 star; an all-lost result yields 0.
        /// </summary>
        public int StarsForLanded(int landed)
        {
            if (landed >= starThreshold3)
            {
                return 3;
            }

            if (landed >= starThreshold2)
            {
                return 2;
            }

            if (landed >= starThreshold1)
            {
                return 1;
            }

            return 0;
        }
    }
}
