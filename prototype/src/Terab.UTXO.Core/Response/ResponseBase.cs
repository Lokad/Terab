
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// The common basis for all responses which includes general information
    /// on the client and the request.
    /// This helps identifying responses and unifying their structure which
    /// serves for their serialization and de-serialization necessary for
    /// the communication with the client.
    /// </summary>
    public abstract class ResponseBase
    {
        // TODO: [vermorel] Those fields should be used named structure.
        public uint RequestId { get; }
        public uint ClientId { get; }
        public MessageType ResponseType { get; }

        // * 3 because space is needed for ReqId, ClientId and 
        // length of the message which is at the beginning of any message.
        // +1 counts the byte that indicates whether a message is sharded or not.
        public static int BaseSizeInBytes => sizeof(uint) * 3 + sizeof(MessageType) + 1;

        protected ResponseBase(uint requestId, uint clientId, MessageType responseType)
        {
            RequestId = requestId;
            ClientId = clientId;
            ResponseType = responseType;
        }
    }
}
