// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using Xunit;

namespace Terab.Lib.Tests.Chains
{
    public class BlockHandleTests
    {
        [Fact]
        public void AliasHandleUndefined()
        {
            var rand = new Random(42);

            for (var i = 0; i < 50000; i++)
            {
                var mask = new ClientId((uint)rand.Next()).Mask;

                var handle = BlockAlias.Undefined.ConvertToBlockHandle(mask);
                Assert.False(handle.IsDefined);
                Assert.Equal(BlockHandle.Undefined, handle);

                var alias = BlockHandle.Undefined.ConvertToBlockAlias(mask);
                Assert.False(alias.IsDefined);
                Assert.Equal(BlockAlias.Undefined, alias);
            }
        }

        [Fact]
        public void AliasHandleRoundTrip()
        {
            var rand = new Random(42);

            for (var i = 0; i < 70000; i++)
            {
                for (var j = 0; j < 64; j++)
                {
                    var alias = new BlockAlias(i, j);

                    Assert.True(alias.IsDefined);
                    Assert.Equal(i, alias.BlockHeight);
                    Assert.Equal(j, alias.SubIndex);

                    var mask = new ClientId((uint)rand.Next()).Mask;

                    var handle = alias.ConvertToBlockHandle(mask);

                    Assert.True(handle.IsDefined);

                    var aliasBis = handle.ConvertToBlockAlias(mask);

                    Assert.Equal(alias, aliasBis);
                }
            }
        }
    }
}