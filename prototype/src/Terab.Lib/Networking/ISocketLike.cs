// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib.Networking
{
    /// <summary>
    /// Clarifies that the implementing object is behaving like a socket.
    /// This allows the tests to use a MockSocket to avoid the need for a real
    /// network.
    /// </summary>
    /// <remarks>
    /// The semantic of 'ISocketLike' is partially aligned with
    /// https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socket
    ///
    /// Unlike 'Socket', both 'Send()' and 'Receive()' are blocking
    /// until the whole buffer if respectively sent or received. This
    /// simplifies most of the code interfaced with 'ISocketLike'.
    /// </remarks>
    public interface ISocketLike
    {
        /// <summary>
        /// Indicates how many bytes are available to be read from the socket.
        /// </summary>
        int Available { get; }

        /// <summary>
        /// Indicates whether the socket is connected to a client.
        /// </summary>
        bool Connected { get; }

        /// <summary>
        /// Sends the data in the given 'Span' over the network.
        /// </summary>
        void Send(Span<byte> bufferOut);

        /// <summary>
        /// Blocking call until all the 'Span' has been filled with data from the network.
        /// </summary>
        void Receive(Span<byte> bufferIn);

        /// <summary>
        /// Closes the socket.
        /// </summary>
        void Close();
    }
}
