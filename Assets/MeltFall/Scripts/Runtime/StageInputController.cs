using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace MeltFall
{
    /// <summary>
    /// Translates single-touch input into aim + hold-fire for the <see cref="LiquidGun"/>
    /// (spec §9, plan §7). On press it raycasts from the camera through the pointer to the stage
    /// and calls <see cref="LiquidGun.BeginFire"/>; on move it updates the aim; on release it ends
    /// firing. Extra simultaneous touches are ignored until the tracked touch releases.
    /// Allocation-free per-frame.
    /// </summary>
    public sealed class StageInputController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera stageCamera;
        [SerializeField] private LiquidGun liquidGun;

        [Tooltip("Layers the aim ray tests against to find the stage impact point.")]
        [SerializeField] private LayerMask stageMask = ~0;

        [Tooltip("Max aim ray distance (world units) used when no surface is hit.")]
        [SerializeField] private float maxAimDistance = 100f;

        // Tracks the finger currently controlling the stream. -1 = none, -2 = mouse/pointer fallback.
        private const int NoTouch = -1;
        private const int MouseTouch = -2;

        private int activeTouchId = NoTouch;
        private bool firingActive;

        /// <summary>Latest aim ray in world space (valid while firing).</summary>
        public Ray CurrentAimRay { get; private set; }

        /// <summary>Latest resolved impact point in world space (valid while firing).</summary>
        public Vector3 CurrentAimPoint { get; private set; }

        /// <summary>True while a firing touch is being tracked.</summary>
        public bool IsFiring => firingActive;

        private void Awake()
        {
            if (stageCamera == null)
            {
                stageCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (stageCamera == null || liquidGun == null)
            {
                return;
            }

            // Prefer touchscreen; fall back to a generic pointer (mouse in editor).
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                HandleTouchscreen(touchscreen);
            }
            else
            {
                HandlePointer();
            }
        }

        private void HandleTouchscreen(Touchscreen touchscreen)
        {
            var touches = touchscreen.touches;

            if (activeTouchId == NoTouch)
            {
                // Look for a newly begun touch to lock onto.
                for (int i = 0; i < touches.Count; i++)
                {
                    TouchControl touch = touches[i];
                    if (touch.press.wasPressedThisFrame)
                    {
                        activeTouchId = touch.touchId.ReadValue();
                        BeginFireAt(touch.position.ReadValue());
                        return;
                    }
                }

                return;
            }

            // We have an active touch: find it and update / release.
            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl touch = touches[i];
                if (touch.touchId.ReadValue() != activeTouchId)
                {
                    continue;
                }

                if (touch.press.wasReleasedThisFrame || !touch.press.isPressed)
                {
                    EndFire();
                    activeTouchId = NoTouch;
                }
                else
                {
                    UpdateFireAt(touch.position.ReadValue());
                }

                return;
            }

            // Tracked touch no longer present (lifted off-screen): stop firing.
            EndFire();
            activeTouchId = NoTouch;
        }

        private void HandlePointer()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                if (firingActive)
                {
                    EndFire();
                    activeTouchId = NoTouch;
                }
                return;
            }

            bool pressed = pointer.press.isPressed;
            Vector2 pos = pointer.position.ReadValue();

            if (activeTouchId == NoTouch)
            {
                if (pointer.press.wasPressedThisFrame)
                {
                    activeTouchId = MouseTouch;
                    BeginFireAt(pos);
                }
            }
            else if (activeTouchId == MouseTouch)
            {
                if (!pressed || pointer.press.wasReleasedThisFrame)
                {
                    EndFire();
                    activeTouchId = NoTouch;
                }
                else
                {
                    UpdateFireAt(pos);
                }
            }
        }

        private void BeginFireAt(Vector2 screenPos)
        {
            Ray ray = BuildAimRay(screenPos);
            CurrentAimRay = ray;
            firingActive = true;
            liquidGun.BeginFire(ray);
        }

        private void UpdateFireAt(Vector2 screenPos)
        {
            Ray ray = BuildAimRay(screenPos);
            CurrentAimRay = ray;
            liquidGun.UpdateAim(ray);
        }

        private void EndFire()
        {
            if (!firingActive)
            {
                return;
            }

            firingActive = false;
            liquidGun.EndFire();
        }

        private Ray BuildAimRay(Vector2 screenPos)
        {
            Ray camRay = stageCamera.ScreenPointToRay(screenPos);

            // Aim from the gun nozzle toward the stage surface the finger points at.
            if (Physics.Raycast(camRay, out RaycastHit hit, maxAimDistance, stageMask, QueryTriggerInteraction.Ignore))
            {
                CurrentAimPoint = hit.point;
            }
            else
            {
                CurrentAimPoint = camRay.origin + camRay.direction * maxAimDistance;
            }

            Vector3 dir = CurrentAimPoint - liquidGun.transform.position;
            if (dir.sqrMagnitude <= 0f)
            {
                dir = camRay.direction;
            }

            return new Ray(liquidGun.transform.position, dir.normalized);
        }
    }
}
