using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Request
{
    /// <summary>
    /// The common basis for all requests which includes general information
    /// on the client and the request.
    /// This helps identifying responses and unifying their structure which
    /// serves for their serialization and deserialization necessary for
    /// the communication with the client.
    /// </summary>
    public abstract class RequestBase
    {
        public uint RequestId { get; }
        public uint ClientId { get; }
        public MessageType RequestType { get; }

        // TODO: [vermorel] Make this computation explicit with the dedicated types
        // * 3 because space is needed for RequestId, ClientId and 
        // length of the message which is at the beginning of any message.
        // +1 counts the byte that indicates whether a message is sharded or not.
        public static int BaseSizeInBytes => sizeof(uint) * 3 + sizeof(MessageType) + 1;

        protected RequestBase(uint requestId, uint clientId, MessageType requestType)
        {
            RequestId = requestId;
            ClientId = clientId;
            RequestType = requestType;
        }
    }
}
