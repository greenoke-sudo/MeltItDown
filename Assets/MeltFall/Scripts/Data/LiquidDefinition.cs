using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// Per-liquid tunable data (design §12.1). All balance/feel values for a liquid
    /// live here; nothing is hardcoded in runtime code.
    /// </summary>
    [CreateAssetMenu(fileName = "Liquid", menuName = "MeltFall/Liquid Definition")]
    public sealed class LiquidDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "Water";
        [SerializeField] private string displayName = "Water";
        [SerializeField] private Color color = Color.cyan;

        [Header("Economy & Melt")]
        [Tooltip("Fuel consumed per second while firing this liquid.")]
        [SerializeField] private float burnRatePerSecond = 1f;

        [Tooltip("Integrity removed per melt tick on a matched material.")]
        [SerializeField] private float meltPower = 1f;

        [Tooltip("Material ids this liquid correctly (fast) melts.")]
        [SerializeField] private string[] matchedMaterialIds = new string[0];

        [Header("Cosmetic (never drive melt)")]
        [Tooltip("Optional stream / particle VFX prefab. Cosmetic only.")]
        [SerializeField] private GameObject streamVFX;

        [Tooltip("Optional firing loop / impact SFX. Cosmetic only.")]
        [SerializeField] private AudioClip sfx;

        /// <summary>Stable liquid identifier (e.g. Water, Acid, Solvent, Heat).</summary>
        public string Id => id;

        /// <summary>Human-readable label for the selector.</summary>
        public string DisplayName => displayName;

        /// <summary>Button + stream tint.</summary>
        public Color Color => color;

        /// <summary>Fuel consumed per second while firing.</summary>
        public float BurnRatePerSecond => burnRatePerSecond;

        /// <summary>Integrity removed per melt tick on a matched material.</summary>
        public float MeltPower => meltPower;

        /// <summary>Material ids this liquid correctly melts.</summary>
        public string[] MatchedMaterialIds => matchedMaterialIds;

        /// <summary>Optional cosmetic stream VFX prefab (may be null).</summary>
        public GameObject StreamVFX => streamVFX;

        /// <summary>Optional cosmetic SFX (may be null).</summary>
        public AudioClip Sfx => sfx;

        /// <summary>Returns true if this liquid correctly melts the given material id.</summary>
        public bool Melts(string materialId)
        {
            if (string.IsNullOrEmpty(materialId) || matchedMaterialIds == null)
            {
                return false;
            }

            for (int i = 0; i < matchedMaterialIds.Length; i++)
            {
                if (matchedMaterialIds[i] == materialId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
