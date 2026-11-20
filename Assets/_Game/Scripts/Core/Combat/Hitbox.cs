using UnityEngine;

namespace Game.Core.Combat
{
    /// <summary>A trigger collider on an animal marking one hit zone. The owner is the IDamageReceiver above it.</summary>
    public sealed class Hitbox : MonoBehaviour
    {
        public HitZone zone;

        IDamageReceiver owner;
        bool resolved;

        public IDamageReceiver Owner
        {
            get
            {
                if (!resolved) { owner = GetComponentInParent<IDamageReceiver>(); resolved = true; }
                return owner;
            }
        }
    }
}
