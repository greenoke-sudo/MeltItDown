using System;
using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// A dissolvable structural piece (design §12.2). Tracks integrity, applies melt from
    /// a liquid, drives a dissolve amount, and clears itself (shrink + collider-off) before
    /// removal so it can never catch a falling gem (spec §7).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class MeltableMaterial : MonoBehaviour
    {
        // Local, non-tunable dissolve/clear feel constants (not gameplay balance).
        private const float ClearDurationSeconds = 0.25f;
        private const float MinClearScale = 0.001f;

        [Header("Data")]
        [SerializeField] private MaterialDefinition def;

        [Header("Rendering (optional)")]
        [Tooltip("Renderer whose dissolve/cutoff property is driven. May be null.")]
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("Shader float property driven with the dissolve amount (0..1).")]
        [SerializeField] private string cutoffProperty = "_Cutoff";

        [Tooltip("Shader color property driven with the material's dissolve color.")]
        [SerializeField] private string dissolveColorProperty = "_DissolveColor";

        private float integrity;
        private float startScale = 1f;
        private Vector3 startLocalScale = Vector3.one;
        private bool isClearing;
        private bool isCleared;
        private float clearElapsed;
        private Collider[] colliders;
        private MaterialPropertyBlock propertyBlock;
        private int cutoffId;
        private int dissolveColorId;

        /// <summary>The material definition backing this piece (may be assigned in the Inspector).</summary>
        public MaterialDefinition Definition => def;

        /// <summary>Current integrity remaining.</summary>
        public float Integrity => integrity;

        /// <summary>Dissolve progress in 0..1 (1 = fully dissolved).</summary>
        public float DissolveAmount
        {
            get
            {
                float max = def != null ? def.MaxIntegrity : 0f;
                if (max <= 0f)
                {
                    return isCleared ? 1f : 0f;
                }

                return Mathf.Clamp01(1f - integrity / max);
            }
        }

        /// <summary>True once the piece has been removed from play.</summary>
        public bool IsCleared => isCleared;

        /// <summary>Fired once when integrity reaches empty (before the shrink/clear completes).</summary>
        public event Action<MeltableMaterial> Emptied;

        /// <summary>
        /// Fired once per piece the moment it becomes cleared (integrity gone, removed from play).
        /// Static so the arbiter can react to any support clearing without per-piece wiring.
        /// </summary>
        public static event Action<MeltableMaterial> Cleared;

        private void Awake()
        {
            integrity = def != null ? def.MaxIntegrity : 0f;
            startLocalScale = transform.localScale;
            colliders = GetComponents<Collider>();
            propertyBlock = new MaterialPropertyBlock();
            cutoffId = Shader.PropertyToID(cutoffProperty);
            dissolveColorId = Shader.PropertyToID(dissolveColorProperty);
            PushDissolveToRenderer();
        }

        /// <summary>
        /// Applies one melt tick from <paramref name="liquid"/>. On a matched liquid, removes
        /// <c>amountPerTick * meltPower</c> integrity; on a wrong liquid applies the material's
        /// <see cref="WrongLiquidResponse"/>. When integrity empties, begins the clear-out.
        /// </summary>
        public void ApplyMelt(LiquidDefinition liquid, float amountPerTick)
        {
            if (isCleared || isClearing || def == null || liquid == null || amountPerTick <= 0f)
            {
                return;
            }

            if (liquid.Melts(def.Id))
            {
                integrity -= amountPerTick * liquid.MeltPower;
            }
            else
            {
                switch (def.WrongLiquidResponse)
                {
                    case WrongLiquidResponse.Chip:
                        integrity -= amountPerTick * def.ChipFraction;
                        break;
                    case WrongLiquidResponse.Ignore:
                    default:
                        // No integrity change on a wrong-liquid hit.
                        break;
                }
            }

            if (integrity < 0f)
            {
                integrity = 0f;
            }

            PushDissolveToRenderer();

            if (integrity <= 0f)
            {
                BeginClear();
            }
        }

        private void BeginClear()
        {
            if (isClearing || isCleared)
            {
                return;
            }

            isClearing = true;
            clearElapsed = 0f;
            startScale = 1f;

            // Disable colliders immediately so the shrinking husk cannot catch a gem.
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = false;
                    }
                }
            }

            Emptied?.Invoke(this);
        }

        private void Update()
        {
            if (!isClearing || isCleared)
            {
                return;
            }

            clearElapsed += Time.deltaTime;
            float t = ClearDurationSeconds > 0f ? Mathf.Clamp01(clearElapsed / ClearDurationSeconds) : 1f;
            float scale = Mathf.Lerp(1f, MinClearScale, t);
            transform.localScale = startLocalScale * scale;

            if (t >= 1f)
            {
                isCleared = true;
                isClearing = false;
                gameObject.SetActive(false);

                // Guarded by the isClearing/isCleared flags above: this block runs exactly once.
                Cleared?.Invoke(this);
            }
        }

        private void PushDissolveToRenderer()
        {
            if (targetRenderer == null || propertyBlock == null)
            {
                return;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(cutoffId, DissolveAmount);
            if (def != null)
            {
                propertyBlock.SetColor(dissolveColorId, def.DissolveColor);
            }
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
