// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// The request ID is intended as a mechanism to let the client properly
    /// associates the requests it sends with the responses it receives.
    /// Server-side, the request IDs are simply copied from requests into
    /// the response counterparts, withing any semantic attached to it.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("Rid:{" + nameof(Value) + "}")]
    public struct RequestId
    {
        public const int SizeInBytes = sizeof(uint);

        public readonly uint Value;

        public static RequestId MaxRequestId = new RequestId(UInt32.MaxValue);

        public static RequestId MinRequestId = new RequestId(UInt32.MinValue);

        public RequestId(uint value)
        {
            Value = value;
        }
    }
}