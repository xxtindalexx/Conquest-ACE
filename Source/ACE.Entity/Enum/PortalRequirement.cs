namespace ACE.Entity.Enum
{
    /// <summary>
    /// Portal requirement types for custom portal restrictions
    /// Used by both PortalReqType and PortalReqType2 properties
    /// </summary>
    public enum PortalRequirement
    {
        None        = 0,
        CreatureAug = 1,
        ItemAug     = 2,
        LifeAug     = 3,
        Enlighten   = 4,
        QuestBonus  = 5,
    }
}
