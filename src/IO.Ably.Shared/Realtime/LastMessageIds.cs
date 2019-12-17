using System.Linq;
using IO.Ably.Types;

namespace IO.Ably.Realtime
{
    internal class LastMessageIds
    {
        public static LastMessageIds Empty = new LastMessageIds();

        public string LastMessageId { get; }

        public string ProtocolMessageChannelSerial { get; }

        private LastMessageIds()
        {
        }

        private LastMessageIds(string lastMessageId, string channelSerial)
        {
            LastMessageId = lastMessageId;
            ProtocolMessageChannelSerial = channelSerial;
        }

        public static LastMessageIds Create(ProtocolMessage message)
        {
            if (message.Action != ProtocolMessage.MessageAction.Message
                || message.Messages == null
                || message.Messages.Length == 0)
            {
                return Empty;
            }

            return new LastMessageIds(message.Messages.Last().Id, message.ChannelSerial);
        }
    }
}