using UnityEngine;

namespace Game.Weapons
{
    /// <summary>
    /// Everything one gun needs in the hunter's hands (owner, 2026-11-27: more guns will come; each must plug in):
    /// its handling and ballistics, the first-person model (arms and gun on one rig), its animations as overrides of
    /// the shared first-person animator, where it sits, where the muzzle is and how it moves. A new gun is a new
    /// model, its clips for the standard roles and one of these; no code.
    /// </summary>
    /// <remarks>
    /// Standard animation roles (states of FP_Weapon.controller, each with a placeholder clip the kit overrides):
    /// Ready (draw), Idle (looping), Fire (shot and its bolt or pump cycle), Reload, ReloadEmpty. Reloads play at the
    /// speed that makes them last the definition's reload time.
    /// </remarks>
    [CreateAssetMenu(menuName = "Game/Weapons/Weapon Kit", fileName = "Kit_")]
    public sealed class WeaponKit : ScriptableObject
    {
        public WeaponDefinition definition;
        [Tooltip("Arms and gun on one rig, with an Animator, materials set, on any layer (the mount sets the first-person layer).")]
        public GameObject viewModel;
        [Tooltip("The gun's clips for the standard roles: an override of FP_Weapon.controller.")]
        public AnimatorOverrideController animations;

        [Header("Placement (in the camera's space)")]
        [Tooltip("The model under the holder (rigs made as a whole body put the arms 1.6 m above their feet).")]
        public Vector3 modelOffset;
        public Vector3 hipPosition;
        public Vector3 aimPosition = new Vector3(0f, 0f, 0.05f);
        [Tooltip("Muzzle flash position on the holder, just past the barrel tip in the idle pose.")]
        public Vector3 muzzle = new Vector3(0.05f, -0.03f, 0.8f);

        [Header("Feel")]
        public WeaponMotionSettings motion = new WeaponMotionSettings();
    }
}
