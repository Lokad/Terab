using System.Net;

namespace Terab.UTXO.Core.Networking
{
    // TODO: [vermorel] Properties should be used, do not expose public fields.
    // TODO: [vermorel] 'Configuration' sould be renamed something more specific, ex: 'TerabServerConfiguration'.
    // TODO: [vermorel] Many fields unused below, they should be cleaned-up.

    /// <summary>
    /// Lists some parameters that are configurable in the Terab application.
    /// </summary>
    public class Configuration
    {
        // Configure entrance socket.
        public int ListenBacklogSize;

        public IPAddress ListenAddress;

        public int ListenPort;

        // Configure outbound communication
        /// <summary>
        /// Send buffer within the socket.
        /// </summary>
        public int SocketSendBufferSize; // TODO: [vermorel] Renamed as 'Max' something

        /// <summary>
        /// Send buffer within ClientConnection.
        /// </summary>
        public int ClientConnectionSendBufferSize;

        /// <summary>
        /// Receive buffer within the Socket.
        /// </summary>
        /// <remarks>
        /// There will be no additional receive buffer with considerable 
        /// size in the ClientConnection. This is intendedly 
        /// unsymmetrical to the Send situation.
        /// </remarks>
        public int SocketReceiveBufferSize;

        // Configure intra-application communication
        public int ThreadInboxSize;

        /// <summary>
        /// Distance to head in a blockchain from when on blocks
        /// cannot have children anymore and become pruneable.
        /// </summary>
        public static int FreezeDepth;
    }
}
