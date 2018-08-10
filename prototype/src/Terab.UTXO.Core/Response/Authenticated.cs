using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Request;

namespace Terab.UTXO.Core.Response
{
    /// <summary>
    /// Response indicating to the client whether authentication has
    /// been successful or not.
    /// TODO: needs to be furnished with more fields to make this useful.
    /// Corresponding request is <see cref="Authenticate"/> // TODO: if used ..
    /// </summary>
    public class Authenticated : ResponseBase
    {
        public static readonly MessageInfo Info =
            new MessageInfo(false, SizeInBytes, true, MessageType.EverythingOk, "EverythingOk");

        public static int SizeInBytes => BaseSizeInBytes + sizeof(bool);

        public bool IsAuthenticated { get; }

        /// <summary>
        /// Length of the message
        /// </summary>
        public Authenticated(uint requestId, uint clientId, bool isAuthenticated) : base(requestId, clientId,
            MessageType.Authenticated)
        {
            IsAuthenticated = isAuthenticated;
        }
    }
}