using System;
using ACE.Server.Network.Enum;

namespace ACE.Server.Network.GameMessages
{
    [AttributeUsage(AttributeTargets.Method)]
    public class GameMessageAttribute : Attribute
    {
        public OutboundGameMessageOpcode Opcode { get; }
        public SessionState State { get; }

        public GameMessageAttribute(OutboundGameMessageOpcode opcode, SessionState state)
        {
            Opcode = opcode;
            State  = state;
        }
    }
}
