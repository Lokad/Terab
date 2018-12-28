// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Terab.Lib.Messaging
{
    /// <summary>
    /// The 'ClientId's are purely server-side. Each incoming request gets
    /// decorated by the receiving socket (server-side) with a 'ClientId'
    /// attached to the socket itself. This 'ClientId' is then used to
    /// properly route the response back to the original client.
    /// </summary>
    /// <remarks>
    /// In all requests, the client is not expected to do anything with
    /// the 'ClientId' except to leave it to its default value.
    ///
    /// 'IEquatable' is implemented in order to avoid a boxing behavior
    /// from the 'Dictionary' using in the 'DispatchController' to select
    /// the controller associated to the message.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("Cid:{" + nameof(Value) + "}")]
    public struct ClientId : IEquatable<ClientId>
    {
        public const int SizeInBytes = sizeof(uint);

        public static readonly ClientId MaxClientId = new ClientId(UInt32.MaxValue);

        public static readonly ClientId MinClientId = new ClientId(UInt32.MinValue);

        private static int _sequence;

        private readonly uint _value;

        public uint Value => _value;

        public BlockHandleMask Mask => new BlockHandleMask(_value);

        public static ClientId Next()
        {
            // Interlocked operation enables thread-safe generation of distinct
            // client IDs. This is not strictly needed ('Next' is called only
            // from the dispatcher thread, when a new connection is accepted,
            // but the cost is negligible.
            var newValue = Interlocked.Increment(ref _sequence);
            if (newValue == 0) // here's the limitation of this strategy.
            {
                throw new InvalidOperationException("Too many client connections generated.");
            }

            return new ClientId((uint) newValue);
        }

        /// <summary> Intended for unit tests only. </summary>
        internal ClientId(uint value)
        {
            _value = value;
        }

        public bool Equals(ClientId other)
        {
            return _value == other._value;
        }

        public override int GetHashCode()
        {
            return (int) _value;
        }

        /// <summary> Intended for logging capabilities. </summary>
        public override string ToString()
        {
            return _value.ToString();
        }
    }
}