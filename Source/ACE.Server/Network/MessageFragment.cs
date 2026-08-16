using System;
using System.IO;
using System.Runtime.CompilerServices;

using ACE.Server.Network.GameMessages;

using log4net;

namespace ACE.Server.Network
{
    internal class MessageFragment
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

        public OutboundGameMessage Message { get; private set; }

        public uint Sequence { get; set; }

        public ushort Index { get; set; }

        public ushort Count { get; set; }

        public int DataLength => (int)Message.Data.Length;

        public int DataRemaining { get; private set; }

        private readonly string sessionIdentifier;

        public int NextSize
        {
            get
            {
                var dataSize = DataRemaining;
                if (dataSize > PacketFragment.MaxFragmentDataSize)
                    dataSize = PacketFragment.MaxFragmentDataSize;
                return PacketFragmentHeader.HeaderSize + dataSize;
            }
        }

        public int TailSize => PacketFragmentHeader.HeaderSize + (DataLength % PacketFragment.MaxFragmentDataSize);

        public bool TailSent { get; private set; }

        public MessageFragment(OutboundGameMessage message, uint sequence, string sessionIdentifier = null)
        {
            Message = message;
            this.sessionIdentifier = sessionIdentifier;
            DataRemaining = DataLength;
            Sequence = sequence;
            Count = (ushort)(Math.Ceiling((double)DataLength / PacketFragment.MaxFragmentDataSize));
            Index = 0;
            if (Count == 1)
                TailSent = true;
            packetLog.DebugFormat($"Sequence {sequence}, count {Count}, DataRemaining {DataRemaining}");
        }

        public ServerPacketFragment GetTailFragment()
        {
            var index = (ushort)(Count - 1);
            TailSent = true;
            return CreateServerFragment(index);
        }

        public ServerPacketFragment GetNextFragment()
        {
            return CreateServerFragment(Index++);
        }

        private void LogFragmentFailure(ushort index, int position, int bytesRequested, string reason)
        {
            log.Error($"FragmentFailure | Session={sessionIdentifier ?? "unknown"} | Opcode={Message.Opcode} | Group={Message.Group} | FragmentIndex={index} | FragmentCount={Count} | DataLength={DataLength} | Position={position} | BytesRequested={bytesRequested} | StreamPosition={Message.Data.Position} | MessageHash={RuntimeHelpers.GetHashCode(Message)} | StreamHash={RuntimeHelpers.GetHashCode(Message.Data)} | ThreadId={Environment.CurrentManagedThreadId} | Reason={reason}");
        }

        private ServerPacketFragment CreateServerFragment(ushort index)
        {
            packetLog.DebugFormat($"Creating ServerFragment for index {index}");

            var position = index * PacketFragment.MaxFragmentDataSize;

            if (index >= Count)
            {
                LogFragmentFailure(index, position, 0, $"index {index} is greater than computed count {Count}");
                throw new InvalidOperationException($"Passed index {index} is greater then computed count {Count}");
            }

            if (position > DataLength)
            {
                LogFragmentFailure(index, position, 0, $"index {index} computes to invalid position, datalength {DataLength}");
                throw new InvalidOperationException($"Passed index {index} computes to invalid position size, datalength: {DataLength}");
            }

            if (DataRemaining <= 0)
            {
                LogFragmentFailure(index, position, 0, "no data remaining");
                throw new InvalidOperationException("There is no data remaining");
            }

            var dataToSend = DataLength - position;
            if (dataToSend > PacketFragment.MaxFragmentDataSize)
                dataToSend = PacketFragment.MaxFragmentDataSize;

            if (DataRemaining < dataToSend)
            {
                LogFragmentFailure(index, position, dataToSend, "more data to send than data remaining");
                throw new InvalidOperationException("More data to send then data remaining!");
            }

            byte[] data;
            try
            {
                Message.Data.Seek(position, SeekOrigin.Begin);
                data = new byte[dataToSend];
                Message.Data.Read(data, 0, dataToSend);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException)
            {
                LogFragmentFailure(index, position, dataToSend, ex.Message);
                throw;
            }

            // Build ServerPacketFragment structure
            ServerPacketFragment fragment = new ServerPacketFragment(data);
            fragment.Header.Sequence = Sequence;
            fragment.Header.Id = 0x80000000;
            fragment.Header.Count = Count;
            fragment.Header.Index = index;
            fragment.Header.Queue = (ushort)Message.Group;

            DataRemaining -= dataToSend;
            packetLog.DebugFormat($"Done creating ServerFragment for index {index}. After reading {dataToSend} DataRemaining {DataRemaining}");
            return fragment;
        }
    }
}
