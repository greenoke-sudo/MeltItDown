using System.Collections;
using MeltFall;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MeltFall.UI
{
    /// <summary>
    /// HUD readout for the shared <see cref="FuelTank"/> (plan §6.2). Subscribes to the tank's
    /// change/emptied events (no per-frame polling), fills a bar, updates a label, and reflects the
    /// coarse <see cref="FuelLevel"/> bucket (Normal / Low / Empty). In Low it flashes the fill via a
    /// coroutine; in Empty it renders fully drained. Presentation only — no gameplay logic and no
    /// hardcoded tunables (colors, flash timing, and the low threshold are serialized).
    /// </summary>
    public sealed class FuelGaugeView : MonoBehaviour
    {
        [Header("Fill target (assign one)")]
        [Tooltip("Filled image (Image.type = Filled). Uses fillAmount for the 0..1 fraction.")]
        [SerializeField] private Image fillImage;

        [Tooltip("Optional slider alternative to the fill image.")]
        [SerializeField] private Slider fillSlider;

        [Header("Label")]
        [Tooltip("Optional numeric / fraction readout.")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Format applied to the label. {0}=current, {1}=max, {2}=percent (0..100).")]
        [SerializeField] private string labelFormat = "{2:0}%";

        [Header("Level colors (serialized — no hardcoded tunables)")]
        [SerializeField] private Color normalColor = new Color(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private Color lowColor = new Color(1f, 0.7f, 0.1f, 1f);
        [SerializeField] private Color lowFlashColor = new Color(1f, 0.25f, 0.1f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        [Header("Low-fuel threshold")]
        [Tooltip("If a runtime tuning is bound via BindTuning, its LowFuelThreshold01 is used instead.")]
        [Range(0f, 1f)]
        [SerializeField] private float lowFuelThreshold01 = 0.25f;

        [Header("Low-fuel flash")]
        [Tooltip("Seconds for one full flash cycle while Low.")]
        [SerializeField] private float flashPeriodSeconds = 0.6f;

        private FuelTank fuelTank;
        private LoopTuningConfig tuning;
        private FuelLevel level = FuelLevel.Normal;
        private Coroutine flashRoutine;

        /// <summary>Wires the gauge to a fuel tank. Safe to re-bind (unhooks the previous tank).</summary>
        public void Bind(FuelTank tank)
        {
            if (fuelTank == tank)
            {
                return;
            }

            Unhook();
            fuelTank = tank;

            if (isActiveAndEnabled)
            {
                Hook();
                if (fuelTank != null)
                {
                    Refresh(fuelTank.Current, fuelTank.Max);
                }
            }
        }

        /// <summary>Optional: bind the runtime tuning so the low-fuel threshold comes from config.</summary>
        public void BindTuning(LoopTuningConfig config)
        {
            tuning = config;
            if (fuelTank != null)
            {
                Refresh(fuelTank.Current, fuelTank.Max);
            }
        }

        private void OnEnable()
        {
            Hook();
            if (fuelTank != null)
            {
                Refresh(fuelTank.Current, fuelTank.Max);
            }
        }

        private void OnDisable()
        {
            Unhook();
            StopFlash();
        }

        private void Hook()
        {
            if (fuelTank == null)
            {
                return;
            }

            fuelTank.Changed += OnFuelChanged;
            fuelTank.Emptied += OnFuelEmptied;
        }

        private void Unhook()
        {
            if (fuelTank == null)
            {
                return;
            }

            fuelTank.Changed -= OnFuelChanged;
            fuelTank.Emptied -= OnFuelEmptied;
        }

        private void OnFuelChanged(float current, float max)
        {
            Refresh(current, max);
        }

        private void OnFuelEmptied()
        {
            SetLevel(FuelLevel.Empty);
        }

        private void Refresh(float current, float max)
        {
            float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (fillImage != null)
            {
                fillImage.fillAmount = fraction;
            }

            if (fillSlider != null)
            {
                fillSlider.value = fraction;
            }

            if (label != null)
            {
                label.SetText(labelFormat, current, max, fraction * 100f);
            }

            SetLevel(EvaluateLevel(current, fraction));
        }

        private FuelLevel EvaluateLevel(float current, float fraction)
        {
            if (current <= 0f)
            {
                return FuelLevel.Empty;
            }

            float threshold = tuning != null ? tuning.LowFuelThreshold01 : lowFuelThreshold01;
            return fraction <= threshold ? FuelLevel.Low : FuelLevel.Normal;
        }

        private void SetLevel(FuelLevel newLevel)
        {
            level = newLevel;

            switch (level)
            {
                case FuelLevel.Normal:
                    StopFlash();
                    ApplyColor(normalColor);
                    break;

                case FuelLevel.Low:
                    StartFlash();
                    break;

                case FuelLevel.Empty:
                    StopFlash();
                    ApplyColor(emptyColor);
                    break;
            }
        }

        private void StartFlash()
        {
            if (flashRoutine == null && isActiveAndEnabled)
            {
                flashRoutine = StartCoroutine(FlashLoop());
            }
        }

        private void StopFlash()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
        }

        private IEnumerator FlashLoop()
        {
            while (true)
            {
                float t = 0f;
                float period = Mathf.Max(0.01f, flashPeriodSeconds);
                while (t < period)
                {
                    t += Time.unscaledDeltaTime;
                    // 0..1..0 triangle across the period.
                    float phase = Mathf.PingPong(t / (period * 0.5f), 1f);
                    ApplyColor(Color.Lerp(lowColor, lowFlashColor, phase));
                    yield return null;
                }
            }
        }

        private void ApplyColor(Color color)
        {
            if (fillImage != null)
            {
                fillImage.color = color;
            }

            if (fillSlider != null && fillSlider.fillRect != null)
            {
                Image sliderFill = fillSlider.fillRect.GetComponent<Image>();
                if (sliderFill != null)
                {
                    sliderFill.color = color;
                }
            }
        }
    }
}
