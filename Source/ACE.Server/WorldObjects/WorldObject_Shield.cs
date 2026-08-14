using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        public bool Sentinel
        {
            get => GetProperty(PropertyBool.Sentinel) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.Sentinel); else SetProperty(PropertyBool.Sentinel, value); }
        }

        public bool CriticalBlock
        {
            get => GetProperty(PropertyBool.CriticalBlock) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.CriticalBlock); else SetProperty(PropertyBool.CriticalBlock, value); }
        }

        public bool GlancingBlow
        {
            get => GetProperty(PropertyBool.GlancingBlow) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.GlancingBlow); else SetProperty(PropertyBool.GlancingBlow, value); }
        }
    }
}
