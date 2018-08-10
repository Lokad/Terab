namespace Terab.UTXO.Core.Messaging
{
    /// <summary>
    /// Indicates the type of message, either request or response.
    /// </summary>
    /// <remarks>
    /// A new octet is started for every new type of message (client errors
    /// vs server errors vs response/requests in case everything goes well).
    /// </remarks>
    public enum MessageType
    {
        // Before or at client or message reception - server fault

        /// <summary>
        /// Indicates that the working threads have no capacity to treat the
        /// incoming request.
        /// </summary>
        /// <remarks>
        /// This guarantees worst-case performance
        /// as opposed to slowly performing worse and worse as new requests
        /// queue up.
        /// </remarks>
        ServerBusy = 1,

        /// <summary>
        /// Indicates that so many clients are already connected 
        /// that the maximum number of active connections has been reached.
        /// </summary>
        NoMoreSpaceForClients = 2,

        // Message/client faults

        /// <summary>
        /// Indicates that the <see cref="ClientServerMessage.TryGetLength"/> of
        /// an inbound message exceeded the
        /// <see cref="ClientServerMessage.MaxSizeInBytes"/>.
        /// </summary>
        /// <remarks>
        /// This probably means that the message stream has been corrupted.
        /// </remarks>
        RequestTooLong = 8,

        /// <summary>
        /// Indicates that the transmitted message is shorter than
        /// <see cref="ClientServerMessage.MinSizeInBytes"/> bytes which
        /// means that the essential fields are not present.
        /// </summary>
        /// <remarks>
        /// This probably means that the message stream has been corrupted.
        /// </remarks>
        RequestTooShort = 9,

        /// <summary>
        /// Indicates that the client did not respect the rule of leaving a
        /// part of the incoming message empty.
        /// </summary>
        /// <remarks>
        /// This empty part is used for internal organization by the server.
        /// </remarks>
        ClientIdFieldNotEmpty = 10,

        /// <summary>
        /// Indicates that the client is not reading its messages quickly enough
        /// and the server's outbound buffer is full.
        /// </summary>
        OutBufferFull = 11,

        // Request Response pairs in case everything goes well
        // All request enums are even numbers while all response enums are odd.

        /// <summary>
        /// Client authentication demand. The corresponding response
        /// type is <see cref="Authenticated"/>
        /// </summary>
        Authenticate = 16,
        /// <summary>
        /// Informs the client whether authentication has passed or not. This
        /// response type corresponds to
        /// <see cref="Authenticate"/>.
        /// </summary>
        Authenticated = 17,

        /// <summary>
        /// Opens a block that can still be modified
        /// in the sense that TXs can be added in the Sozu table
        /// for this block.
        /// The corresponding response type is
        /// <see cref="EverythingOk"/>.
        /// </summary>
        OpenBlock = 18,
        /// <summary>
        /// Response to the request
        /// <see cref="OpenBlock"/>.
        /// </summary>
        OpenedBlock = 19,

        /// <summary>
        /// Requests a block alias of a given block Id.
        /// The corresponding response type is
        /// <see cref="BlockHandle"/>.
        /// </summary>
        GetBlockHandle = 20,
        
        /// <summary>
        /// Given the identifier of an uncommitted block returned in
        /// <see cref="GetBlockInfo"/>, returns the handle of the block which
        /// can be used for communication with the Terab application.
        /// </summary>
        GetUncommittedBlockHandle = 22,
        /// <summary>
        /// The handle of a block. The corresponding request is either
        /// <see cref="GetBlockHandle"/> or
        /// <see cref="GetUncommittedBlockHandle"/>.
        /// </summary>
        BlockHandle = 23,

        /// <summary>
        /// Given a block handle of an either committed or uncommitted block,
        /// returns the block information stored in Terab via either
        /// <see cref="UncommittedBlockInfo"/> or
        /// <see cref="CommittedBlockInfo"/>.
        /// </summary>
        GetBlockInfo = 24,

        /// <summary>
        /// Information about an uncommitted block identified by its alias,
        /// consisting of the parent alias, temporary block identifier and its
        /// block height. This response type corresponds to
        /// <see cref="GetBlockInfo"/>.
        /// </summary>
        UncommittedBlockInfo = 25,

        /// <summary>
        /// Information about a committed block identified by its alias,
        /// consisting of the parent alias, a block identifier and its block
        /// height. This response type corresponds to
        /// <see cref="GetBlockInfo"/>.
        /// </summary>
        CommittedBlockInfo = 27,

        /// <summary>
        /// Given two blocks asks whether one is the ancestor of the other in
        /// the current state of the blockchain, essentially meaning whether
        /// they are on the same branch.
        /// The corresponding response type is
        /// <see cref="AncestorResponse"/>.
        /// </summary>
        IsAncestor = 28,
        /// <summary>
        /// Boolean response to the request
        /// <see cref="IsAncestor"/>.
        /// </summary>
        AncestorResponse = 29,

        /// <summary>
        /// Given a block requests whether this block can be deleted from the
        /// current blockchain. This depends on whether the block is on a side
        /// chain, whether it has children and whether it still has references
        /// in the sozu table.
        /// The corresponding response type is
        /// <see cref="PruneableResponse"/>.
        /// </summary>
        IsPruneable = 30,
        /// <summary>
        /// Boolean response to the request
        /// <see cref="IsPruneable"/>.
        /// </summary>
        PruneableResponse = 31,

        /// <summary>
        /// Follows an <see cref="OpenBlock"/> request. Freezes an
        /// existing block in the sense that no more TXs can be
        /// added in the Sozu table for this block.
        /// The corresponding response type is
        /// <see cref="EverythingOk"/>.
        /// </summary>
        CommitBlock = 32,
        /// <summary>
        /// Indicates that everything went smoothly and no problems were
        /// encountered.
        /// This flag is returned if there is no information to be sent back, as
        /// in the case of a <see cref="CommitBlock"/>.
        /// </summary>
        EverythingOk = 33
    }
}
