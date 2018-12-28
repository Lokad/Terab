// Copyright Lokad 2018 under MIT BCH.
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Contains all fields that are present at the beginning of any message.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MessageHeader
    {
        public static readonly int SizeInBytes = sizeof(MessageHeader);

        public int MessageSizeInBytes;
        public RequestId RequestId;
        public ClientId ClientId;
        public MessageKind MessageKind;

        public MessageHeader(int messageSizeInBytes, RequestId requestId, ClientId clientId, MessageKind messageKind)
        {
            MessageSizeInBytes = messageSizeInBytes;
            RequestId = requestId;
            ClientId = clientId;
            MessageKind = messageKind;
        }
    }
}