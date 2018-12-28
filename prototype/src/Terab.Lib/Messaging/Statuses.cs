// Copyright Lokad 2018 under MIT BCH.
namespace Terab.Lib.Messaging
{
    public enum OpenBlockStatus : byte
    {
        Success = 0,
        ParentNotFound = 1,
    }

    public enum CommitBlockStatus : byte
    {
        Success = 0,
        BlockNotFound = 1,
        BlockIdMismatch = 2,
    }

    public enum GetBlockHandleStatus : byte
    {
        Success = 0,
        BlockNotFound = 1,
    }

    public enum GetCoinStatus : byte
    {
        Success = 0,
        OutpointNotFound = 1,
    }

    public enum ChangeCoinStatus : byte
    {
        Success = 0,
        OutpointNotFound = 1,
        InvalidContext = 2,
        InvalidBlockHandle = 3,
    }

    public enum ProtocolErrorStatus
    {
        NoError = 0,

        /// <summary>
        /// Indicates that the working threads have no capacity to treat the
        /// incoming request.
        /// </summary>
        /// <remarks>
        /// This guarantees worst-case performance as opposed to slowly
        /// performing worse and worse as new requests queue up.
        /// </remarks>
        ServerBusy = 1,

        /// <summary>
        /// Indicates that so many clients are already connected that the
        /// maximum number of active connections has been reached.
        /// </summary>
        TooManyActiveClients = 2,

        /// <summary>
        /// The message received carried an unknown message type.
        /// </summary>
        InvalidMessageKind = 4,

        /// <summary>
        /// Indicates that the length of an inbound message exceeded the
        /// <see cref="Constants.MaxRequestSize"/>.
        /// </summary>
        /// <remarks>
        /// This probably means that the message stream has been corrupted.
        /// </remarks>
        RequestTooLong = 8,

        /// <summary>
        /// Indicates that the transmitted message is shorter than the message
        /// header, which  means that the essential fields are not present.
        /// </summary>
        /// <remarks>
        /// This probably means that the message stream has been corrupted.
        /// </remarks>
        RequestTooShort = 9,

        UnspecifiedError = int.MaxValue
    }
}