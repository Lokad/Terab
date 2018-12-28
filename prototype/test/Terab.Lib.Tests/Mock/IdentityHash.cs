// Copyright Lokad 2018 under MIT BCH.
using Terab.Lib.Coins;
using Terab.Lib.Messaging;

namespace Terab.Lib.Tests.Mock
{
    /// <summary>
    /// Intended for testing purposes.
    /// </summary>
    public unsafe class IdentityHash : IOutpointHash
    {
        public ulong Hash(ref Outpoint outpoint)
        {
            ulong hash = 0;
            for (var i = 0; i < 8; i++)
                hash += ((ulong)outpoint.TxId[i]) << (i * 8);

            return hash;
        }
    }
}
