using Game.Animals;
using Game.Core.Combat;
using Game.Player;
using Game.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Short messages for what just happened in the hunt (clean kill, wounded, missed, attacked, escaped) and a red
    /// flash when an animal hits the hunter. The full result screen comes with the hunt flow.
    /// </summary>
    public sealed class HuntMessageView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Text label;
        [SerializeField] Graphic damageFlash;
        [SerializeField] float showSeconds = 4f;

        WeaponController weapon;
        FirstPersonController hunter;
        float hideAt, flash;

        void OnEnable()
        {
            AnimalHealth.AnyHit += OnAnimalHit;
            AnimalBrain.AnyStateChanged += OnAnimalState;
        }

        void OnDisable()
        {
            AnimalHealth.AnyHit -= OnAnimalHit;
            AnimalBrain.AnyStateChanged -= OnAnimalState;
            if (weapon) weapon.Fired -= OnFired;
            if (weapon) weapon.Empty -= OnEmpty;
            if (hunter) hunter.Attacked -= OnAttacked;
        }

        void Update()
        {
            if (!weapon && (weapon = FindFirstObjectByType<WeaponController>())) { weapon.Fired += OnFired; weapon.Empty += OnEmpty; }
            if (!hunter && (hunter = FindFirstObjectByType<FirstPersonController>())) hunter.Attacked += OnAttacked;
            group.alpha = Mathf.Clamp01((hideAt - Time.time) / 0.5f);
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 1.5f);
            var c = damageFlash.color;
            damageFlash.color = new Color(c.r, c.g, c.b, flash * 0.45f);
        }

        void Show(string text)
        {
            label.text = text;
            hideAt = Time.time + showSeconds;
        }

        void OnFired(ShotResult r)
        {
            if (!r.hitAnimal) Show(r.hitSomething ? $"Missed. The shot hit the ground at {r.distance:0} m." : "Missed.");
        }

        void OnEmpty() => Show("Empty. Reload.");

        void OnAnimalHit(AnimalHealth animal, HitInfo hit, HitOutcome outcome)
        {
            switch (outcome)
            {
                case HitOutcome.CleanKill: Show($"Clean kill: a heart and lung shot at {hit.distance:0} m. The farm is safe tonight."); break;
                case HitOutcome.Kill: Show($"The wolf is down ({hit.zone.ToString().ToLowerInvariant()} hit, {hit.distance:0} m)."); break;
                case HitOutcome.Wounded: Show("Hit, but not a vital shot. The wolf is wounded: follow the blood trail."); break;
                case HitOutcome.WoundedLeg: Show("Leg hit. The wolf is limping away: follow the blood trail."); break;
            }
        }

        void OnAnimalState(AnimalBrain animal, AnimalState state)
        {
            if (state == AnimalState.Charge) Show("The wounded wolf is charging!");
            else if (state == AnimalState.Gone) Show(animal.Recovered ? "The wolf recovered from its wound and escaped." : "The wolf ran off.");
        }

        void OnAttacked(Vector3 from)
        {
            flash = 1f;
            Show("The wolf knocked you down. It is running again.");
        }
    }
}
