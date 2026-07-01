using MeltFall;
using UnityEngine;

namespace MeltFall.UI
{
    /// <summary>
    /// A faint impact marker at the stream's landing point (plan §6.5). Reflects
    /// <see cref="AimIndicatorState"/> Hidden / Showing. The gun / input controller drives it via
    /// <see cref="SetAimPoint"/> plus <see cref="Show"/> / <see cref="Hide"/>. Presentation only —
    /// no gameplay logic, no hardcoded tunables (the marker object is serialized).
    /// </summary>
    public sealed class AimIndicatorView : MonoBehaviour
    {
        [Header("Marker")]
        [Tooltip("The marker object moved to the aim point and toggled by state.")]
        [SerializeField] private Transform marker;

        [Tooltip("Optional: keep the marker upright / facing the camera. If set, the marker looks away from this transform.")]
        [SerializeField] private Transform faceTarget;

        private AimIndicatorState state = AimIndicatorState.Hidden;

        /// <summary>Current visibility state.</summary>
        public AimIndicatorState State => state;

        private void Awake()
        {
            ApplyState();
        }

        private void OnDisable()
        {
            // Ensure the marker is not left visible if the view is torn down mid-show.
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }
        }

        /// <summary>Moves the marker to a world-space landing point.</summary>
        public void SetAimPoint(Vector3 worldPoint)
        {
            if (marker == null)
            {
                return;
            }

            marker.position = worldPoint;

            if (faceTarget != null)
            {
                Vector3 dir = marker.position - faceTarget.position;
                if (dir.sqrMagnitude > 0f)
                {
                    marker.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }
        }

        /// <summary>Shows the marker (enters Showing).</summary>
        public void Show()
        {
            state = AimIndicatorState.Showing;
            ApplyState();
        }

        /// <summary>Shows the marker at a point in one call.</summary>
        public void Show(Vector3 worldPoint)
        {
            SetAimPoint(worldPoint);
            Show();
        }

        /// <summary>Hides the marker (enters Hidden).</summary>
        public void Hide()
        {
            state = AimIndicatorState.Hidden;
            ApplyState();
        }

        private void ApplyState()
        {
            if (marker != null)
            {
                marker.gameObject.SetActive(state == AimIndicatorState.Showing);
            }
        }
    }
}
