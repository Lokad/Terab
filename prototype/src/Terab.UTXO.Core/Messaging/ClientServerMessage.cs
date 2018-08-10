using System;

namespace Terab.UTXO.Core.Messaging
{
    // TODO: [vermorel] I suggest to introduce a 'ref struct RequestSpan' to add some typing clarity.

    /// <summary>
    /// Describes the format of binary messages exchanged with the client.
    /// </summary>
    /// <remarks>
    /// Each message is supposed to be of the following format:
    ///  - The first 4 bytes specify the length of the message, including itself
    ///  - The next 4 bytes specify the client request number
    ///  - The next 4 bytes specify the client identifier.
    ///      This is given by the server, and if a message is received with
    ///         anything written into that space, the server will reply with an
    ///         Error message to the client.
    ///  - The next byte indicates whether a message is sharded and in the
    ///         future this byte will possibly encode more internal information
    ///  - The next 4 bytes contain information to identify the request/response
    ///         type
    ///  - From the 17th byte on follows the payload of the message. If the
    ///         message is sharded, the payload will have to start with the key.
    /// </remarks>
    public static class ClientServerMessage
    {
        /// <summary>
        /// No messages above that size are allowed to be written.
        /// Larger than <see cref="Consensus.MaxScriptSizeInBytes"/>
        /// to be able to put it in.
        /// </summary>
        public const int MaxSizeInBytes = 11000;
        public const int MinSizeInBytes = PayloadStart;


        /// <summary>
        /// Serie of format starting positions constants
        /// </summary>
        public const int LengthStart = 0;
        public const int LengthSizeInBytes = sizeof(int);

        public const int RequestIdStart = LengthStart + LengthSizeInBytes;
        public const int RequestIdSizeInBytes = sizeof(int);

        public const int ClientIdStart = RequestIdStart + RequestIdSizeInBytes;
        public const int ClientIdSizeInBytes = sizeof(int);

        public const int ShardedIndStart = ClientIdStart + ClientIdSizeInBytes;
        public const int ShardedIndSizeInBytes = sizeof(byte);

        public const int MessageTypeStart = ShardedIndStart + ShardedIndSizeInBytes;
        public const int MessageTypeSizeInBytes = sizeof(int);

        public const int PayloadStart = MessageTypeStart + MessageTypeSizeInBytes;

        /// <summary>
        /// Returns the length of a message, in bytes.
        /// </summary>
        /// <remarks>
        /// Message length is always stored as the first four bytes, in little-endian order.
        /// </remarks>
        public static bool TryGetLength(ReadOnlySpan<byte> request, out int size)
        {
            // to read the length of the message, the request has to be at least of length 4
            if (request.Length >= LengthStart + LengthSizeInBytes)
            {
                size = BitConverter.ToInt32(request);
                return true;
            }

            size = 0;
            return false;
        }

        /// <summary>
        /// Parse request number of given message. Request numbers start at 1.
        /// </summary>
        /// <param name="request">
        /// The entire message is expected, not just the request ID part.
        /// </param>
        public static uint GetRequestId(ReadOnlySpan<byte> request)
        {
            // to read a request ID, the request has to be at least of length 8
            // to accomodate this element and all the elements that come before
            if (request.Length >= RequestIdStart + RequestIdSizeInBytes)
                return BitConverter.ToUInt32(request.Slice(RequestIdStart, RequestIdSizeInBytes));
            throw new ArgumentException(
                $"Provided request of length {request.Length}, should have been at least {RequestIdStart + RequestIdSizeInBytes}.");
        }

        /// <summary>
        /// Permits reading out the ClientId.
        /// </summary>
        public static ClientId GetClientId(ReadOnlySpan<byte> request)
        {
            if (request.Length >= ClientIdStart + ClientIdSizeInBytes
                ) // to read a client ID, the request has to be at least of length 12
                // to accomodate this element and all the elements that come before
            {
                return ClientId.ReadFrom(request.Slice(ClientIdStart, ClientIdSizeInBytes));
            }

            throw new ArgumentException(
                $"Provided request of length {request.Length}, should have been at least {ClientIdStart + ClientIdSizeInBytes}.");
        }

        /// <summary>
        /// Permits reading out the RequestId and ClientId together.
        /// </summary>
        public static ulong GetId(ReadOnlySpan<byte> request)
        {
            if (request.Length >= ClientIdStart + ClientIdSizeInBytes
                ) // to read a request and client ID, the request has to be at least of length 12
                // to accomodate this element and all the elements that come before
            {
                return BitConverter.ToUInt64(request.Slice(RequestIdStart, RequestIdSizeInBytes + ClientIdSizeInBytes));
            }

            throw new ArgumentException(
                $"Provided request of length {request.Length}, should have been at least {ClientIdStart + ClientIdSizeInBytes}.");
        }

        /// <summary>
        /// Provides information if the message is intended to be sharded.
        /// </summary>
        public static bool IsSharded(ReadOnlySpan<byte> request)
        {
            if (request.Length >= ShardedIndStart + ShardedIndSizeInBytes)
            {
                // to decide whether a message is sharded, the request has to be at least of length 13
                // to accomodate this element and all the elements that come before
                byte flags = request[ShardedIndStart];
                return flags >> 7 == 1;
            }

            throw new ArgumentException(
                $"Provided request of length {request.Length}, should have been at least {ShardedIndStart + ShardedIndSizeInBytes}.");
        }

        /// <summary>
        /// Permits identifying the request type of the provided message.
        /// </summary>
        public static MessageType GetMessageType(ReadOnlySpan<byte> request)
        {
            if (request.Length >= MessageTypeStart + MessageTypeSizeInBytes
                ) // to read a request type, the request has to be at least of length 17
                // to accomodate this element and all the elements that come before
            {
                return (MessageType) BitConverter.ToInt32(request.Slice(MessageTypeStart, MessageTypeSizeInBytes));
            }

            throw new ArgumentException(
                $"Provided request of length {request.Length}, should have been at least {MessageTypeStart + MessageTypeSizeInBytes}.");
        }

        /// <summary>
        /// Returns the first byte of the key of the provided message.
        /// </summary>
        /// <param name="request">
        /// The entire message is expected, not just the first part of the key.
        /// </param>
        public static byte FirstKeyByte(ReadOnlySpan<byte> request)
        {
            // to read the first key byte, the request has to be at least of length 18
            // to accomodate this element and all the elements that come before
            if (request.Length >= PayloadStart + sizeof(byte))
                return request[PayloadStart];
            throw new ArgumentException(
                $"Provided request of length {request.Length}, should have been at least {PayloadStart + sizeof(byte)}.");
        }

        public static void SetHeader(
            Span<byte> buffer,
            int length,
            ulong id, // TODO: [vermorel] Use a named 'struct' instead.
            byte isSharded, // TODO: [vermorel] 'isSharded' should be a 'Bool' .
            MessageType response)
        {
            if (buffer.Length < MessageTypeStart + MessageTypeSizeInBytes)
                throw new ArgumentException(
                    $"Provided request of length {buffer.Length}, should have been at least MessageTypeStart + sizeof(int).");

            BitConverter.TryWriteBytes(buffer.Slice(LengthStart, LengthSizeInBytes), length);
            BitConverter.TryWriteBytes(buffer.Slice(RequestIdStart, sizeof(ulong)), id);
            BitConverter.TryWriteBytes(buffer.Slice(ShardedIndStart, ShardedIndSizeInBytes), isSharded);
            BitConverter.TryWriteBytes(buffer.Slice(MessageTypeStart, MessageTypeSizeInBytes), (int) response);
        }
    }
}