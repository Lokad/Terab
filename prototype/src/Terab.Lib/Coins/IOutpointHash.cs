// Copyright Lokad 2018 under MIT BCH.
using Terab.Lib.Messaging;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Abstraction of a hash function with a predefined secret.
    /// It protected Terab against non trivial denial-of-service attack.
    /// </summary>
    public interface IOutpointHash
    {
        ulong Hash(ref Outpoint outpoint);
    }
}
