using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// Per-material tunable data (design §12.1). Holds the material's integrity,
    /// its one correct liquid, and its wrong-liquid response. Nothing hardcoded in code.
    /// </summary>
    [CreateAssetMenu(fileName = "Material", menuName = "MeltFall/Material Definition")]
    public sealed class MaterialDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "Sand";

        [Header("Durability")]
        [Tooltip("Starting integrity (\"HP\") of a piece of this material.")]
        [SerializeField] private float maxIntegrity = 100f;

        [Tooltip("Id of the one liquid that dissolves this material fast.")]
        [SerializeField] private string correctLiquidId = "Water";

        [Header("Appearance")]
        [Tooltip("Tint applied to the dissolve edge while melting.")]
        [SerializeField] private Color dissolveColor = Color.white;

        [Header("Wrong-liquid response")]
        [SerializeField] private WrongLiquidResponse wrongLiquidResponse = WrongLiquidResponse.Ignore;

        [Tooltip("Near-zero fraction of the incoming amount removed on a wrong-liquid hit (only used when response = Chip).")]
        [Range(0f, 1f)]
        [SerializeField] private float chipFraction = 0f;

        /// <summary>Stable material identifier (e.g. Sand, Metal, Stone, Ice).</summary>
        public string Id => id;

        /// <summary>Starting integrity of a fresh piece.</summary>
        public float MaxIntegrity => maxIntegrity;

        /// <summary>Id of the liquid that correctly melts this material.</summary>
        public string CorrectLiquidId => correctLiquidId;

        /// <summary>Dissolve-edge tint while melting.</summary>
        public Color DissolveColor => dissolveColor;

        /// <summary>Behavior when hit by a non-matching liquid.</summary>
        public WrongLiquidResponse WrongLiquidResponse => wrongLiquidResponse;

        /// <summary>Near-zero fraction chipped on a wrong-liquid hit (0..1).</summary>
        public float ChipFraction => chipFraction;
    }
}
