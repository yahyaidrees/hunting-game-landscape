using Game.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Rounds left in the magazine, or "Reloading".</summary>
    public sealed class AmmoView : MonoBehaviour
    {
        [SerializeField] Text label;

        WeaponController weapon;
        int shownAmmo = -1;
        bool shownReloading;

        void Update()
        {
            if (!weapon) weapon = FindFirstObjectByType<WeaponController>();
            if (!weapon) return;
            if (weapon.Ammo == shownAmmo && weapon.IsReloading == shownReloading) return;
            shownAmmo = weapon.Ammo;
            shownReloading = weapon.IsReloading;
            label.text = shownReloading ? "Reloading" : $"{shownAmmo} / {weapon.Weapon.magazineSize}";
        }
    }
}
