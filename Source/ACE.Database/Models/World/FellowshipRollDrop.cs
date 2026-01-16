namespace ACE.Database.Models.World
{
    /// <summary>
    /// Data structure for fellowship roll drop configuration
    /// </summary>
    public class FellowshipRollDrop
    {
        public uint MobWcid { get; set; }
        public uint PetWcid { get; set; }
        public float Probability { get; set; }
    }
}
