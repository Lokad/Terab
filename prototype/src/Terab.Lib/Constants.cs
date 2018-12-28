// Copyright Lokad 2018 under MIT BCH.
namespace Terab.Lib
{
    /// <summary>
    /// Gathering of misc numbers that drives the design of Terab, but
    /// aren't worth putting in the configuration.
    /// </summary>
    public static class Constants
    {
        private const int _KB = 1024;
        private const int _MB = 1024 * 1024;

        // === NETWORKING ===
        // ==================

        public const int DefaultPort = 8338;

        /// <summary> Timeout in milliseconds for the socket emission. </summary>
        public const int SocketSendTimeoutMs = 20 * 1000;

        /// <summary> Size in bytes of inbound socket buffer for handling request from client to Terab. </summary>
        public const int SocketReceiveBufferSize = 1 * _MB;

        /// <summary> Size in bytes of outbound socket buffer for handling responses from Terab to the client. </summary>
        public const int SocketSendBufferSize = 1 * _MB;

        /// <summary> In bytes. </summary>
        public const int MaxRequestSize = 16 * _KB;

        /// <summary> In bytes. </summary>
        public const int MaxResponseSize = 16 * _KB;

        /// <summary> Maximum number of active connections from clients to Terab. </summary>
        public const int MaxActiveConnections = 32;


        // === INNER TOPOLOGY ===
        // ======================

        /// <summary> Number of distinct coin controllers (used to shard the workload). </summary>
        public const int CoinControllerCount = 16;

        /// <summary> Size in bytes of the bounded inbox of the block controller. </summary>
        public const int ChainControllerInboxSize = 64 * _KB;

        /// <summary> Size in bytes of the bounded inbox of each coin controller. </summary>
        public const int CoinControllerInboxSize = 128 * _KB;

        /// <summary> Size in bytes of the bounded inbox (outgoing responses) of each connection controller. </summary>
        public const int ConnectionControllerOutboxSize = 1 * _MB;

        /// <summary> Size in bytes of the bounded outbox of the dispatch controller. </summary>
        public const int DispatchControllerOutboxSize = 1 * _MB;


        // === STORAGE ===
        // ===============

        /// <summary> Fixed allocation for the SipHash secret. </summary>
        public const int SecretStoreFileSize = 16;

        /// <summary> Fixed allocation for the memory mapped file backing the blockchain. </summary>
        public const int ChainStoreFileSize = 64 * _MB;

        /// <summary> Size in bytes of the journal used by a coin controller
        /// (as many allocations as there are coin controllers). </summary>
        public const int CoinStoreJournalCapacity = 1 * _MB;

        /// <summary> Size in bytes of each sector in the layer 1 of the
        /// pack store backing the Sozu table. </summary>
        public const int CoinStoreLayer1SectorSize = 4096;

        /// <summary> Number of 'OutpointSig' needed to consider the probabilistic
        /// filter as overflowed, hence, later disabled.</summary>
        public const int CoinStoreOutpointSigMaxCount = 1500;

        /// <summary> Distance to the tip of the blockchain where orphan
        /// blocks are considered as permanently orphaned, and thus
        /// eligible to pruning. </summary>
        public const int BlockPruneLimitDistance = 100;

        /// <summary> If data storage hardware used for the layer 1 supports atomic
        /// writes over 4096 bytes, then journalization can be skipped when write is
        /// restricted to a single sector of the layer 1. </summary>
        public const bool SkipJournalizationWithAtomicWrite = true;

    }
}