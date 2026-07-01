using UnityEngine;

namespace MeltFall
{
    /// <summary>
    /// A trigger volume marking a safe landing region (spec §6). A gem that comes to rest
    /// inside a safe zone (and fell the minimum distance) counts as landed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SafeZone : MonoBehaviour
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

            // ClosestPoint returns the input point when it is inside the collider.
            Vector3 closest = zoneCollider.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude <= Mathf.Epsilon;
        }
    }
}
