using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// Indicates to the client that his request
    /// has succeeded. This response is returned if no response
    /// information has to be returned. This is the case for the
    /// request <see cref="CommitBlock"/>, and it might apply to more
    /// in the future.
    /// </summary>
    public class EverythingOkResponse : ResponseBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, true, MessageType.EverythingOk, "EverythingOk");

        public static int SizeInBytes => BaseSizeInBytes;

        public EverythingOkResponse(uint requestId, uint clientId) :
            base(requestId, clientId, MessageType.EverythingOk)
        {

        }
    }
}
