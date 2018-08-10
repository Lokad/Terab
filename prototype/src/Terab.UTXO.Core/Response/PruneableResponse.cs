using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// Indicates a binary response to the request <see cref="IsAncestor"/>.
    /// </summary>
    public class PruneableResponse : ResponseBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, true, MessageType.PruneableResponse, "PruneableResponse");

        public bool Answer { get; }

        public static int SizeInBytes => BaseSizeInBytes + sizeof(bool);

        public PruneableResponse(
            uint requestId, uint clientId, bool answer) :
            base(requestId, clientId, MessageType.PruneableResponse)
        {
            Answer = answer;
        }
    }
}
