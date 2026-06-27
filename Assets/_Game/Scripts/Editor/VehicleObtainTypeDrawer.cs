using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Vehicles.Editor
{
    /// <summary>
    /// Odin drawer for <see cref="VehicleObtainType"/> that keeps <see cref="VehicleObtainType.Free"/>
    /// mutually exclusive with the earn/pay flags (ByGold / ByWatchAds / DistanceMilestoneKm). A car is
    /// either free or unlockable through one or more of the other methods - never both.
    ///
    /// "Last toggle wins": ticking Free clears every other flag; ticking any other flag clears Free. This
    /// is decided by diffing the new mask against the previous one to see which flag the user just enabled,
    /// which is why this is a drawer (it has the old value on hand) rather than a one-shot OnValidate fixup.
    /// </summary>
    public class VehicleObtainTypeDrawer : OdinValueDrawer<VehicleObtainType>
    {
        private const VehicleObtainType NonFreeFlags =
            VehicleObtainType.ByGold | VehicleObtainType.ByWatchAds | VehicleObtainType.DistanceMilestoneKm;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var entry = ValueEntry;
            VehicleObtainType current = entry.SmartValue;

            EditorGUI.BeginChangeCheck();
            var selected = (VehicleObtainType)SirenixEditorFields.EnumDropdown(label, current);
            if (EditorGUI.EndChangeCheck())
            {
                entry.SmartValue = Enforce(current, selected);
                entry.Values.ForceMarkDirty();
            }
        }

        // Looks at which flags were just turned ON (next minus previous) and keeps Free exclusive from the
        // rest based on that, so the flag the user most recently clicked is the one that is honored.
        private static VehicleObtainType Enforce(VehicleObtainType previous, VehicleObtainType next)
        {
            VehicleObtainType added = next & ~previous;

            // Just enabled Free -> Free becomes the only obtain type.
            if ((added & VehicleObtainType.Free) != 0)
                return VehicleObtainType.Free;

            // Just enabled an earn/pay flag while Free was set -> drop Free, keep the rest.
            if ((added & NonFreeFlags) != 0 && (next & VehicleObtainType.Free) != 0)
                return next & ~VehicleObtainType.Free;

            return next;
        }
    }
}
