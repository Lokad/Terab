// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using Xunit;

namespace Terab.Lib.Tests.Messaging
{
    public class BlockHandleAndAliasTests
    {

        [Fact(Skip = "[vermorel] 2018-11-29, fail because of redefinition of genesis value")]
        public void GenesisParentHandle()
        {
            var rand = new Random(42);
            var randInt = rand.Next();

            var handle1 = new BlockHandle(unchecked ((uint)randInt) ^ BlockHandle.DefaultMask);
            var handle2 = BlockAlias.GenesisParent.ConvertToBlockHandle(new BlockHandleMask(unchecked((uint)randInt)));
            
            Assert.Equal(handle1, handle2);
        }

        [Fact]
        public void ConvertToBlockAlias()
        {
            var alias = new BlockAlias(100, 1);
            var handle = alias.ConvertToBlockHandle(new BlockHandleMask(0x5678abcd));
            var aliasFromHandle = handle.ConvertToBlockAlias(new BlockHandleMask(0x5678abcd));

            Assert.Equal(alias, aliasFromHandle);
        }

        [Fact]
        public void GetPriorAlias()
        {
            var alias = new BlockAlias(100, 1);
            var priorAlias = alias.GetPriorAlias(50);
            Assert.Equal(priorAlias, new BlockAlias(50, 0));

            priorAlias = alias.GetPriorAlias(100);
            Assert.Equal(priorAlias, new BlockAlias(0, 0));

            priorAlias = alias.GetPriorAlias(101);
            Assert.Equal(priorAlias, BlockAlias.GenesisParent);

            priorAlias = alias.GetPriorAlias(0);
            Assert.Equal(priorAlias, new BlockAlias(100, 0));

            // negative values are not allowed anymore
            //Assert.Throws<ArgumentException>(() => alias.GetPriorAlias(-1));
            //Assert.Throws<ArgumentException>(() => alias.GetPriorAlias(-100));
        }
    }
}
