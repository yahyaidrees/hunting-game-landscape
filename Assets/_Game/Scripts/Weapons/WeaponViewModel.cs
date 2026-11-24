using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// Plays the first-person arms-and-rifle animations from what the rifle does: draw when the hunt starts, the
    /// shot with its bolt cycle, a reload (a longer one when the magazine was empty), idle in between.
    /// </summary>
    public sealed class WeaponViewModel : MonoBehaviour
    {
        public const string Ready = "Ready", Idle = "Idle", Fire = "Fire", Reload = "Reload", ReloadEmpty = "ReloadEmpty";

        [SerializeField] WeaponController weapon;
        [SerializeField] Animator animator;
        [SerializeField] float fireFade = 0.03f, reloadFade = 0.12f;

        void Awake()
        {
            weapon.Fired += OnFired;
            weapon.ReloadStarted += OnReload;
        }

        void OnDestroy()
        {
            if (!weapon) return;
            weapon.Fired -= OnFired;
            weapon.ReloadStarted -= OnReload;
        }

        // once, when the hunt scene starts (the model is hidden and shown again with the scope; that must not redraw)
        void Start()
        {
            if (animator && animator.runtimeAnimatorController) animator.Play(Ready, 0, 0f);
        }

        void OnFired(ShotResult shot) => animator.CrossFadeInFixedTime(Fire, fireFade, 0, 0f);

        // the magazine is refilled when the reload ends, so Ammo still tells whether it was empty
        void OnReload() => animator.CrossFadeInFixedTime(weapon.Ammo == 0 ? ReloadEmpty : Reload, reloadFade);
    }
}
