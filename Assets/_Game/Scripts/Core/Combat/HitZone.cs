using UnityEngine;

namespace Game.Core.Combat
{
    /// <summary>Where a shot lands (plan section 6, "Shooting": heart and lung shots kill quickly, body or leg shots wound).</summary>
    public enum HitZone
    {
        Leg = 0,
        Body = 1,
        Head = 2,
        Vital = 3   // heart and lungs: the clean, ethical shot
    }

    public readonly struct HitInfo
    {
        public readonly HitZone zone;
        public readonly Vector3 point;
        public readonly Vector3 direction;
        public readonly float distance;
        public readonly float damageMultiplier;

        public HitInfo(HitZone zone, Vector3 point, Vector3 direction, float distance, float damageMultiplier)
        {
            this.zone = zone;
            this.point = point;
            this.direction = direction;
            this.distance = distance;
            this.damageMultiplier = damageMultiplier;
        }
    }

    /// <summary>Something that can be shot (an animal).</summary>
    public interface IDamageReceiver
    {
        void ReceiveHit(HitInfo hit);
    }
}
