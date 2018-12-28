// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Coins;
using Terab.Lib.Messaging;
using Xunit;

namespace Terab.Lib.Tests.Coins
{
    public unsafe class OutpointSigTests
    {
        [Fact]
        public void From()
        {
            var rand = new Random(42);

            var outpoint = new Outpoint();
            for (var i = 0; i < 32; i++)
                outpoint.TxId[i] = 123;

            var sig = OutpointSig.From((ulong) rand.Next());

            // Dummy, mostly for coverage
            Assert.True(sig.Value != 0);
        }
    }
}
