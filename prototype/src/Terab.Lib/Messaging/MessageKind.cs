// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Linq;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Indicates the type of message, either request or response.
    /// </summary>
    /// <remarks>
    /// A new octet is started for every new type of message (client errors
    /// vs server errors vs response/requests in case everything goes well).
    ///
    /// All request enums are even numbers while all response enums are odd.
    /// </remarks>
    public enum MessageKind : int
    {
        // === CONNECTION CONTROLLER (0 - 15) ===
        // ======================================

        /// <summary> Error at the protocol level. </summary>
        ProtocolErrorResponse = 1,

        /// <summary> Not implemented (placeholder for now). </summary>
        Authenticate = 2,

        /// <summary> Not implemented (placeholder for now). </summary>
        AuthenticateResponse = 3,

        /// <summary> Client request to terminate the connection. </summary>
        CloseConnection = 4,

        /// <summary> Upon  connection closure. </summary>
        CloseConnectionResponse = 5,


        // === CHAIN CONTROLLER (16 - 63) ===
        // ==================================

        /// <summary> Create new uncommitted - hence mutable - block. </summary>
        OpenBlock = 16,

        /// <summary> Upon block creation. </summary>
        OpenBlockResponse = 17,

        /// <summary> Finalize block as immutable. </summary>
        CommitBlock = 18,

        /// <summary> Upon block commit. </summary>
        CommitBlockResponse = 19,

        /// <summary> Get block handle for a block (either committed or uncommitted).</summary>
        GetBlockHandle = 20,

        /// <summary> Upon request for a block handle. </summary>
        BlockHandleResponse = 21,

        /// <summary>
        /// Given a block handle of an either committed or uncommitted block,
        /// returns the block information.
        /// </summary>
        GetBlockInfo = 22,

        /// <summary> Upon request for block info. </summary>
        BlockInfoResponse = 23,


        // === COIN CONTROLLER (64 - infinity) ====
        // ========================================

        /// <summary> Request a coin to be read. </summary>
        GetCoin = 64,

        /// <summary> A successful response to <see cref="GetCoin"/>. </summary>
        GetCoinResponse = 65,

        /// <summary> Request a coin to be produced. </summary>
        ProduceCoin = 66,

        /// <summary> Result of a <see cref="ProduceCoin"/> request. </summary>
        ProduceCoinResponse = 67,

        /// <summary> Request a coin to be consumed. </summary>
        ConsumeCoin = 68,

        /// <summary> Result of a <see cref="ConsumeCoin"/> request. </summary>
        ConsumeCoinResponse = 69,

        /// <summary> Request a coin event (production and/or consumption) to be removed. </summary>
        RemoveCoin = 70,

        /// <summary> Result of a <see cref="RemoveCoin"/> request. </summary>
        RemoveCoinResponse = 71,
    }

    public static class MessageKindExtensions
    {
        private static readonly bool[] Defined;

        static MessageKindExtensions()
        {
            Defined = new bool[256];
            foreach (var kind in Enum.GetValues(typeof(MessageKind)).Cast<MessageKind>())
            {
                Defined[(byte) (int) kind] = true;
            }
        }

        public static bool IsDefined(this MessageKind kind)
        {
            return Defined[(byte) (int) kind];
        }

        public static bool IsResponse(this MessageKind kind)
        {
            return ((int) kind & 0x01) != 0;
        }

        public static bool IsForCoinController(this MessageKind kind)
        {
            return (int) kind >= 64;
        }
    }
}