using Terab.UTXO.Core.Messaging;
using Terab.UTXO.Core.Response;

namespace Terab.UTXO.Core.Request
{
    // TODO: [vermorel] It took me a while to understand that all request messages where of constant sizes.
    // Hence, the 'static' Length. This should be documented and clarified in a 'ReadMe.md' within this folder.

    /// <summary>
    /// Client request for authentication.
    /// TODO: not sure this is used. If it is, new fields might be necessary to store data and make this class useful
    /// Corresponding response is <see cref="Authenticated"/>.
    /// </summary>
    public class Authenticate : RequestBase
    {
        public static readonly MessageInfo Info = 
            new MessageInfo(false, SizeInBytes, false, MessageType.Authenticate, "Authenticate");

        public static int SizeInBytes => BaseSizeInBytes + 0;

        public Authenticate(uint requestId, uint clientId) : base(requestId, clientId, MessageType.Authenticate)
        {
        }
    }
}