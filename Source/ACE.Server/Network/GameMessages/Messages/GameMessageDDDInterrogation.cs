using ACE.Server.Managers;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageDDDInterrogation : OutboundGameMessage
    {
        public GameMessageDDDInterrogation()
            : base(OutboundGameMessageOpcode.DDD_Interrogation, GameMessageGroup.DatabaseQueue)
        {
            uint productID = 0x1;
            if (PropertyManager.GetBool("allow_highres_dat"))
                productID |= 0x4;

            Writer.Write(1u); // m_dwServersRegion
            Writer.Write(1u); // m_NameRuleLanguage
            Writer.Write(productID); // m_dwProductID
            Writer.Write(2u); // m_SupportedLanguages.Count
                Writer.Write(0u); // Invalid
                Writer.Write(1u); // English
        }
    }
}
