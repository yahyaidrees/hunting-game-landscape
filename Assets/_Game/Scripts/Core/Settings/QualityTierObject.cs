using UnityEngine;

namespace Game.Core.Settings
{
    /// <summary>
    /// Shows a child object only on some graphics tiers, for example the blob shadow under an animal on Low,
    /// where real-time shadows are off (plan section 13).
    /// </summary>
    public sealed class QualityTierObject : MonoBehaviour
    {
        [SerializeField] GameObject target;
        [SerializeField] bool onLow = true, onMedium, onHigh;

        void OnEnable()
        {
            QualityTiers.Applied += Show;
            Show(QualityTiers.HasApplied ? QualityTiers.Current : GameSettings.Tier);
        }

        void OnDisable() => QualityTiers.Applied -= Show;

        void Show(QualityTier tier)
        {
            if (!target) return;
            bool on = tier switch { QualityTier.Low => onLow, QualityTier.Medium => onMedium, _ => onHigh };
            if (target.activeSelf != on) target.SetActive(on);
        }
    }
}
