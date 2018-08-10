using System;
using System.Threading;

namespace Terab.UTXO.Core.Messaging
{
    /// <summary>
    /// Strongly-typed wrapping of (numerical) client connection id.
    /// </summary>
    public struct ClientId : IEquatable<ClientId>
    {
        private static int _sequence;

        private readonly uint _value;

        // temporary strategy of construction of Client IDs. Eventually, we
        // allow for recycling of client IDs, once we can ascertain  all its
        // pending requests have been replied to, or at least flushed out of
        // the various threads inboxes.
        public static ClientId Next()
        {
            // Interlocked operation enables thread-safe generation of distinct
            // client IDs. This is not strictly needed ('Next' is called only
            // from the dispatcher thread, when a new connection is accepted,
            // but the cost is negligible.
            var newValue = Interlocked.Increment(ref _sequence);
            if (newValue == 0) // here's the limitation of this strategy.
            {
                const string msg =
                    "ClientId sequence has rolled (more than 2^32 connections have been made to this Terab instance)";
                throw new InvalidOperationException(msg);
            }

            return new ClientId((uint) newValue);
        }

        public bool IsSpecified => _value != 0;

        private ClientId(uint value)
        {
            // hiding constructor from public sight. The only way to make
            // ClientId where 'IsSpecified' is false is to read it from (cf.
            // ReadFrom) an incoming request, and ensure that it is indeed
            // "not specified"
            _value = value;
        }

        public void WriteTo(Span<byte> buffer)
        {
            if (!BitConverter.TryWriteBytes(buffer, _value))
            {
                throw new Exception("ClientId: destination buffer too small to write");
            }
        }

        public static ClientId ReadFrom(ReadOnlySpan<byte> buffer)
        {
            return new ClientId(BitConverter.ToUInt32(buffer));
        }

        public bool Equals(ClientId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ClientId && Equals((ClientId) obj);
        }

        public override int GetHashCode()
        {
            return (int) _value;
        }

        public static bool operator ==(ClientId left, ClientId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ClientId left, ClientId right)
        {
            return !left.Equals(right);
        }
    }
}