using System;

namespace Terab.UTXO.Core.Networking
{
    /// <summary>
    /// Clarifies that the implementing object is behaving like a socket.
    /// This allows the tests to use a MockSocket to avoid the need for a real
    /// network.
    /// </summary>
    public interface ISocketLike
    {
        /// <summary>
        /// Indicates how many bits are available to be read from the socket.
        /// </summary>
        int Available();

        /// <summary>
        /// Sends the data in the given Span on the net (or acts as if) and
        /// returns how many of them could actually be sent.
        /// </summary>
        int Send(Span<byte> toSend);

        /// <summary>
        /// Writes received bits into the given buffer, indicating how many were
        /// written.
        /// </summary>
        int Receive(Span<byte> bufferIn);

        /// <summary>
        /// Indicates whether the socket is connected to a client.
        /// </summary>
        /// <returns></returns>
        bool Connected();

        /// <summary>
        /// Closes the socket.
        /// </summary>
        void Close();
    }
}
