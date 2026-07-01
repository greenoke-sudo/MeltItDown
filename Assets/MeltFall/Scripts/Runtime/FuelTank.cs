using System;
using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// The shared fuel value — the economy/clock spine (spec §5, design §12.2).
    /// Plain serializable class (not a MonoBehaviour) owned by <see cref="LevelManager"/>.
    /// No mid-level auto-refill. Allocation-free on the hot path.
    /// </summary>
    [Serializable]
    public sealed class FuelTank
    {
        [SerializeField] private float current;
        [SerializeField] private float max;

        private bool emptiedFired;

        /// <summary>Remaining fuel.</summary>
        public float Current => current;

        /// <summary>Starting / maximum fuel.</summary>
        public float Max => max;

        /// <summary>Remaining fraction in 0..1 (0 when max is non-positive).</summary>
        public float Fraction => max > 0f ? Mathf.Clamp01(current / max) : 0f;

        /// <summary>True once the tank has reached zero.</summary>
        public bool IsEmpty => current <= 0f;

        /// <summary>Fired whenever the remaining amount changes. Args: (current, max).</summary>
        public event Action<float, float> Changed;

        /// <summary>Fired exactly once when the tank first reaches empty.</summary>
        public event Action Emptied;

        /// <summary>
        /// Sets the max/starting fuel and fills the tank to it. Resets the emptied latch
        /// and notifies listeners.
        /// </summary>
        public void Initialize(float start)
        {
            max = Mathf.Max(0f, start);
            current = max;
            emptiedFired = false;
            Changed?.Invoke(current, max);
        }

        /// <summary>
        /// Consumes up to <paramref name="amount"/> fuel, clamped at zero.
        /// Returns true if any fuel was actually consumed. Fires <see cref="Changed"/>
        /// on consumption and <see cref="Emptied"/> once when hitting zero.
        /// </summary>
        public bool Consume(float amount)
        {
            if (amount <= 0f || current <= 0f)
            {
                return false;
            }

            float before = current;
            current = Mathf.Max(0f, current - amount);
            bool consumed = current < before;

            if (consumed)
            {
                Changed?.Invoke(current, max);

                if (current <= 0f && !emptiedFired)
                {
                    emptiedFired = true;
                    Emptied?.Invoke();
                }
            }

            return consumed;
        }

        /// <summary>True when the remaining fraction is at or below the given 0..1 threshold.</summary>
        public bool IsLow(float threshold01)
        {
            return Fraction <= threshold01;
        }

        /// <summary>Refills to <see cref="Max"/> (retry), clears the emptied latch, and notifies.</summary>
        public void ResetToStart()
        {
            current = max;
            emptiedFired = false;
            Changed?.Invoke(current, max);
        }
    }
}
