using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Shared setup for Mystery Dye / Powder of Purging — matches pot dye and salvage use-on-target weenies.
    /// </summary>
    internal static class TargetedConsumableTool
    {
        // Pot dye uses SourceContainedTargetContained; SelfOrContained also allows equipped targets.
        public const Usable DefaultItemUseable = Usable.SourceContainedTargetSelfOrContained;

        // Verdalim Dye Pot (8043) defaults
        private const uint DefaultSetup = 0x02000911;
        private const uint DefaultPhysicsTable = 0x3400002B;
        private const int DefaultPhysicsState = 1044;

        public static void ApplyUseOnTargetDefaults(WorldObject wo, ItemType targetType)
        {
            wo.ItemUseable = DefaultItemUseable;
            wo.ItemType = ItemType.CraftCookingBase;

            if (wo.TargetType == null || wo.TargetType == ItemType.None)
                wo.TargetType = targetType;

            if (wo.SetupTableId == 0)
                wo.SetupTableId = DefaultSetup;

            if (wo.PhysicsTableId == 0)
                wo.PhysicsTableId = DefaultPhysicsTable;

            if (wo.GetProperty(PropertyInt.PhysicsState) == null)
                wo.SetProperty(PropertyInt.PhysicsState, DefaultPhysicsState);

            if (wo.EncumbranceVal == null || wo.EncumbranceVal == 0)
                wo.EncumbranceVal = 150;

            if (wo.GetProperty(PropertyInt.Mass) == null || wo.GetProperty(PropertyInt.Mass) == 0)
                wo.SetProperty(PropertyInt.Mass, 50);

            if (wo.GetProperty(PropertyInt.StackUnitEncumbrance) == null)
                wo.SetProperty(PropertyInt.StackUnitEncumbrance, 150);

            if (wo.GetProperty(PropertyInt.StackUnitMass) == null)
                wo.SetProperty(PropertyInt.StackUnitMass, 50);
        }
    }
}
