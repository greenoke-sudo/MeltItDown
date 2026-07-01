using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// A trigger volume marking a hazard kill-floor (spec §6). A gem that comes to rest
    /// inside a hazard zone is lost and does not count toward stars.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HazardZone : MonoBehaviour
    {
        private Collider zoneCollider;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// Returns true if the given world point lies inside this zone's collider bounds.
        /// Uses the collider's closest-point test so non-box shapes are respected.
        /// </summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider>();
            }

            if (zoneCollider == null)
            {
                return false;
            }

            Vector3 closest = zoneCollider.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude <= Mathf.Epsilon;
        }
    }
}
