using System;
using Game.Core.Combat;
using UnityEngine;

namespace Game.Animals
{
    public enum HitOutcome
    {
        /// <summary>Heart or lungs: a quick, ethical kill (bonus).</summary>
        CleanKill,
        /// <summary>Head, or a second wound: dead, but not the clean shot.</summary>
        Kill,
        Wounded,
        WoundedLeg
    }

    /// <summary>
    /// An animal's health and wounds. Heart and lung shots kill at once; a body or leg hit wounds it, it bleeds and
    /// runs, and it slowly recovers while resting unless the hunter finds it first.
    /// </summary>
    public sealed class AnimalHealth : MonoBehaviour, IDamageReceiver
    {
        [SerializeField] AnimalBehaviourSettings settings;

        public float Health { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsWounded => !IsDead && Health < settings.maxHealth - 0.01f;
        public bool LegWound { get; private set; }
        public HitInfo LastHit { get; private set; }

        public event Action<HitOutcome> Hit;
        /// <summary>Any animal hit, for the HUD and hunt results.</summary>
        public static event Action<AnimalHealth, HitInfo, HitOutcome> AnyHit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AnyHit = null;

        void Awake() => Health = settings.maxHealth;

        public void ReceiveHit(HitInfo hit)
        {
            if (IsDead) return;
            LastHit = hit;
            HitOutcome outcome;
            switch (hit.zone)
            {
                case HitZone.Vital:
                    outcome = HitOutcome.CleanKill;
                    break;
                case HitZone.Head:
                    outcome = HitOutcome.Kill;
                    break;
                case HitZone.Leg:
                    Health -= settings.legDamage * hit.damageMultiplier;
                    LegWound = true;
                    outcome = Health <= 0f ? HitOutcome.Kill : HitOutcome.WoundedLeg;
                    break;
                default:
                    Health -= settings.bodyDamage * hit.damageMultiplier;
                    outcome = Health <= 0f ? HitOutcome.Kill : HitOutcome.Wounded;
                    break;
            }
            if (outcome == HitOutcome.CleanKill || outcome == HitOutcome.Kill)
            {
                Health = 0f;
                IsDead = true;
            }
            Hit?.Invoke(outcome);
            AnyHit?.Invoke(this, hit, outcome);
        }

        /// <summary>Called while resting: wounds slowly heal. Returns true when fully recovered.</summary>
        public bool Recover(float dt)
        {
            if (IsDead) return false;
            Health = Mathf.Min(settings.maxHealth, Health + settings.recoverPerSecond * dt);
            if (Health >= settings.maxHealth) LegWound = false;
            return Health >= settings.maxHealth;
        }
    }
}
