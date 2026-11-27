# Weapons

First-person arms and weapons, split into moving parts. Budget: 12k-20k tris, 2048 textures.

## Adding a gun (weapon kits)

Every gun in the hunter's hands is one `WeaponKit` asset (`Assets/_Game/Data/Weapons/Kit_*.asset`). Nothing in code
changes for a new gun.

1. **Model**: arms and gun on one rig, in an FBX under `Art/Weapons/<Gun>/Model/` (Generic rig, materials not
   imported). The HQ FPS Weapons pack's `FP_*.fbx` files are already in this form.
2. **Clips for the standard roles**: `Ready` (draw), `Idle` (loops), `Fire` (shot with its bolt or pump cycle),
   `Reload`, `ReloadEmpty`. They become an `AnimatorOverrideController` of the shared
   `Animation/Controllers/FP_Weapon.controller` (`WeaponKitBuilder.EnsureOverride`). Reloads are sped up or slowed
   down to last the weapon's reload time.
3. **View model prefab** (`Prefabs/Weapons/VM_<Gun>.prefab`): the rig with URP materials, shadows off, and the
   tools in `WeaponKitBuilder`:
   - `SampleVisibility` + `TrimHidden` drop the triangles the camera never sees in any clip;
   - `MapSleeve` + `ThickenSleeve` + `PaintSleeves` dress the bare arms in the hunter's jacket sleeves.
   `GrayboxR01Builder.Rifle.cs` is the worked example (it also adds the hunting rifle's scout scope).
4. **Handling**: a `WeaponDefinition` (magazine, ballistics, scope, sway, recoil, noise).
5. **The kit**: definition, view model, animations, where the model sits (`modelOffset`, `hipPosition`,
   `aimPosition`), the muzzle position and the motion feel (breathing, step bob, look lag, run pose).

In play, `WeaponViewModel.Equip(kit)` swaps to it: model, animations, handling, feel and muzzle flash.

## Hunting rifle (Kit_HuntingRifle)

HQ FPS Weapons hunting rifle (owner's Asset Store licence). The colour map is regraded (warm walnut, blued steel)
into `T_HuntingRifle_AlbedoGraded.png`; delete that file to make it again. Scout scope modelled in code
(`BuildScoutScope`), mounted forward over the handguard so the top-loading clip still clears it.
