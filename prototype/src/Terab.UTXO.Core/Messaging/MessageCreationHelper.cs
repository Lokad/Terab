using System;
using Terab.UTXO.Core.Messaging;

namespace Terab.UTXO.Core.Networking
{
    // TODO: [vermorel] Do not use extension methods when side-effects are involve very unclear.
    // TODO: [vermorel] I suggest to to refactor all those methods as plain static methods.
    // TODO: [vermorel] Rename class as 'ServerClientMessageError'

    /// <summary>
    /// Helper class to create some standard messages to be sent back to
    /// Terab clients, most of whic contain canonical error content.
    /// </summary>
    public static class MessageCreationHelper
    {
        // TODO: [vermorel] I really don't like inline byte initialization unless it's unavoiable.
        // TODO: [vermorel] The message below should be constructed in a static method.

        // All messages will contain
        // 4 bytes of length
        // 4 bytes of request ID
        // 4 bytes of client ID
        // 1 byte  of isSharded
        // 4 bytes of message type
        public static byte[] NoMoreSpaceForClientsMessage =
        {
            0x10, 0x00, 0x00, 0x00, // length fields
            0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF,
            0x08, 0x00, 0x00, 0x00
        };

        public static void EmitRequestTooLong(this Span<byte> buffer, ReadOnlySpan<byte> request)
        {
            if (buffer.Length < ClientServerMessage.PayloadStart)
                throw new ArgumentException($"Output buffer length {buffer.Length} too short.", nameof(buffer));

            ClientServerMessage.SetHeader(
                buffer,
                length: ClientServerMessage.PayloadStart,
                id: ClientServerMessage.GetId(request),
                isSharded:0,
                response: MessageType.RequestTooLong);
        }

        public static void EmitRequestTooShort(this Span<byte> buffer, ReadOnlySpan<byte> request)
        {
            if (buffer.Length < ClientServerMessage.PayloadStart)
                throw new ArgumentException($"Output buffer length {buffer.Length} too short.", nameof(buffer));

            ClientServerMessage.SetHeader(
                buffer,
                length: ClientServerMessage.PayloadStart,
                id: UInt64.MaxValue, // TODO: [vermorel] Very odd. Why not 'GetId(request)'?
                isSharded: 0,
                response: MessageType.RequestTooShort);
        }

        public static void EmitWorkersBusy(this Span<byte> buffer, ReadOnlySpan<byte> request)
        {
            if (buffer.Length < ClientServerMessage.PayloadStart)
                throw new ArgumentException($"Output buffer length {buffer.Length} too short.", nameof(buffer));

            ClientServerMessage.SetHeader(
                buffer,
                length: ClientServerMessage.PayloadStart,
                id: ClientServerMessage.GetId(request),
                isSharded: 0,
                response: MessageType.ServerBusy);
        }

        public static void EmitNoMoreSpaceForClients(this Span<byte> buffer)
        {
            if (buffer.Length < ClientServerMessage.PayloadStart)
                throw new ArgumentException($"Output buffer length {buffer.Length} too short.", nameof(buffer));

            NoMoreSpaceForClientsMessage.CopyTo(buffer);
        }

        // TODO: [vermorel] Don't pass 'request', pass 'GetId(request)' instead
        public static void EmitOutBufferFull(this Span<byte> buffer, ReadOnlySpan<byte> request)
        {
            if (buffer.Length < ClientServerMessage.PayloadStart)
                throw new ArgumentException($"Output buffer length {buffer.Length} too short.", nameof(buffer));

            ClientServerMessage.SetHeader(
                buffer,
                length: ClientServerMessage.PayloadStart,
                id: ClientServerMessage.GetId(request),
                isSharded: 0,
                response: MessageType.OutBufferFull);
        }

        // TODO: [vermorel] Don't pass 'request', pass 'GetId(request)' instead
        public static void EmitClientIdFieldNotEmpty(this Span<byte> buffer, ReadOnlySpan<byte> request)
        {
            if (buffer.Length < ClientServerMessage.PayloadStart)
                throw new ArgumentException($"Output buffer length {buffer.Length} too short.", nameof(buffer));

            ClientServerMessage.SetHeader(
                buffer,
                length: ClientServerMessage.PayloadStart,
                id: ClientServerMessage.GetId(request),
                isSharded: 0,
                response: MessageType.ClientIdFieldNotEmpty);
        }
    }
}
