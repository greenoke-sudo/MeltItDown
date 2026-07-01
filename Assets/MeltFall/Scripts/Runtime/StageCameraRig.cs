using UnityEngine;
using UnityEngine.InputSystem;

namespace MeltFall
{
    /// <summary>
    /// Fixed 3/4 downward-tilted stage camera (spec §9, design §12.4). Applies the authored tilt
    /// and an optional slight parallax pan clamped to <see cref="LoopTuningConfig.ParallaxMax"/>
    /// based on the pointer. No free rotation. Allocation-free.
    /// </summary>
    public sealed class StageCameraRig : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private LoopTuningConfig tuning;

        [Header("References")]
        [Tooltip("The camera transform to orient/pan. Falls back to this transform if null.")]
        [SerializeField] private Transform cameraTransform;

        [Tooltip("Enable slight pointer-driven parallax pan.")]
        [SerializeField] private bool enableParallax = true;

        private Vector3 homePosition;
        private Quaternion homeRotation;
        private bool initialized;

        private void Awake()
        {
            if (cameraTransform == null)
            {
                cameraTransform = transform;
            }

            homePosition = cameraTransform.position;
            homeRotation = cameraTransform.rotation;
            initialized = true;
            ApplyTilt();
        }

        /// <summary>Injects the loop tuning config.</summary>
        public void SetTuning(LoopTuningConfig config)
        {
            tuning = config;
            ApplyTilt();
        }

        /// <summary>Re-centers the camera to its authored home framing and re-applies the tilt.</summary>
        public void ResetView()
        {
            if (!initialized)
            {
                return;
            }

            cameraTransform.position = homePosition;
            cameraTransform.rotation = homeRotation;
            ApplyTilt();
        }

        private void ApplyTilt()
        {
            if (cameraTransform == null || tuning == null)
            {
                return;
            }

            // Preserve the home yaw; apply the authored downward tilt as pitch.
            Vector3 euler = homeRotation.eulerAngles;
            cameraTransform.rotation = Quaternion.Euler(tuning.CameraTiltDegrees, euler.y, euler.z);
            homeRotation = cameraTransform.rotation;
        }

        private void LateUpdate()
        {
            if (!enableParallax || !initialized || tuning == null || cameraTransform == null)
            {
                return;
            }

            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                cameraTransform.position = homePosition;
                return;
            }

            Vector2 pos = pointer.position.ReadValue();
            float w = Screen.width;
            float h = Screen.height;
            if (w <= 0f || h <= 0f)
            {
                return;
            }

            // Map pointer to -1..1 around screen center, then clamp the pan magnitude.
            float nx = Mathf.Clamp((pos.x / w) * 2f - 1f, -1f, 1f);
            float ny = Mathf.Clamp((pos.y / h) * 2f - 1f, -1f, 1f);

            Vector3 offset = new Vector3(nx, ny, 0f) * tuning.ParallaxMax;
            cameraTransform.position = homePosition + cameraTransform.rotation * offset;
        }
    }
}
