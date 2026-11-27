using System;
using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// The gun in the hunter's hands, from its kit: mounts the kit's model (arms and gun) under the motion transform,
    /// gives it the kit's animations, handling and feel, and plays its animations from what the weapon does: draw
    /// when the hunt starts, the shot with its bolt cycle, a reload (a longer one when the magazine was empty), idle
    /// in between. Equip swaps to another kit at any time.
    /// </summary>
    public sealed class WeaponViewModel : MonoBehaviour
    {
        // the standard roles: states of FP_Weapon.controller
        public const string Ready = "Ready", Idle = "Idle", Fire = "Fire", Reload = "Reload", ReloadEmpty = "ReloadEmpty";
        /// <summary>Placeholder clip of each role in FP_Weapon.controller, the one a kit's override replaces.</summary>
        public static string RoleClip(string role) => "Role_" + role;
        public const string ReloadSpeedParameter = "ReloadSpeed", ReloadEmptySpeedParameter = "ReloadEmptySpeed";

        [SerializeField] WeaponController weapon;
        [SerializeField] WeaponKit kit;
        [Tooltip("Where the model hangs: the motion transform under the holder.")]
        [SerializeField] Transform mount;
        [SerializeField] WeaponMotion motion;
        [Tooltip("The muzzle flash on the holder; moved to each kit's muzzle.")]
        [SerializeField] Transform muzzleFlash;
        [Tooltip("The mounted model's animator (the builder mounts the first kit's model ahead of time).")]
        [SerializeField] Animator animator;
        [SerializeField] float fireFade = 0.03f, reloadFade = 0.12f;

        public WeaponKit Kit => kit;
        public event Action<WeaponKit> Equipped;

        void Awake()
        {
            weapon.Fired += OnFired;
            weapon.ReloadStarted += OnReload;
            if (kit) Apply(kit, mountModel: !animator);
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

        /// <summary>Takes up another gun: its model, animations, handling and feel, then draws it.</summary>
        public void Equip(WeaponKit next)
        {
            if (!next || next == kit) return;
            kit = next;
            Apply(next, mountModel: true);
            if (animator && animator.runtimeAnimatorController) animator.Play(Ready, 0, 0f);
        }

        void Apply(WeaponKit k, bool mountModel)
        {
            if (mountModel && k.viewModel && mount)
            {
                if (animator) Destroy(animator.gameObject);
                var model = Instantiate(k.viewModel, mount, false);
                model.name = "ViewModel";
                model.transform.localPosition = k.modelOffset;
                model.transform.localRotation = Quaternion.identity;
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = mount.gameObject.layer;
                animator = model.GetComponentInChildren<Animator>();
            }
            if (animator)
            {
                if (k.animations) animator.runtimeAnimatorController = k.animations;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.keepAnimatorStateOnDisable = true;   // hidden while scoped: carry on where it was
                SetReloadSpeeds(k);
            }
            weapon.Equip(k.definition, k.hipPosition, k.aimPosition);
            if (motion) motion.Settings = k.motion;
            if (muzzleFlash) muzzleFlash.localPosition = k.muzzle;
            Equipped?.Invoke(k);
        }

        // reloads last as long as the weapon's reload time, whatever the clip's length
        void SetReloadSpeeds(WeaponKit k)
        {
            if (!k.animations || !k.definition) return;
            float seconds = Mathf.Max(0.1f, k.definition.reloadSeconds);
            var reload = k.animations[RoleClip(Reload)];
            var empty = k.animations[RoleClip(ReloadEmpty)];
            animator.SetFloat(ReloadSpeedParameter, reload ? reload.length / seconds : 1f);
            animator.SetFloat(ReloadEmptySpeedParameter, empty ? empty.length / seconds : 1f);
        }

        void OnFired(ShotResult shot) => animator.CrossFadeInFixedTime(Fire, fireFade, 0, 0f);

        // the magazine is refilled when the reload ends, so Ammo still tells whether it was empty
        void OnReload() => animator.CrossFadeInFixedTime(weapon.Ammo == 0 ? ReloadEmpty : Reload, reloadFade);
    }
}
