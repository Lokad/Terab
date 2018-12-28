// Copyright Lokad 2018 under MIT BCH.
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Terab.Lib.Chains;
using Terab.Lib.Messaging;
using Terab.Lib.Tests.Mock;
using Xunit;
using Xunit.Abstractions;

namespace Terab.Lib.Tests.Chains
{
    public class ChainStorePerfTests
    {
        /// <summary> Use 600k as a realistic height for the blockchain. </summary>
        private const int BlockHeight = 10000;

        private readonly ILog _log;

        public ChainStorePerfTests(ITestOutputHelper output)
        {
            _log = new XLog(output);
        }

        [Fact]
        public unsafe void DeepChain()
        {
            var store = new ChainStore(new MemoryMappedFileSlim(MemoryMappedFile.CreateNew(null, 
                BlockHeight * 120 + 4096 - (BlockHeight * 120) % 4096)));
            store.Initialize();

            var watch = new Stopwatch();
            watch.Start();

            store.TryOpenBlock(CommittedBlockId.GenesisParent, out var ub);
            
            for (var i = 0; i < BlockHeight; i++)
            {
                var blockId = new CommittedBlockId();
                for (var j = 0; j < 4; j++)
                {
                    blockId.Data[j] = (byte) (j >> (i * 8));
                }
                blockId.Data[5] = 1; // ensure non-zero

                store.TryCommitBlock(ub.Alias, blockId, out var cb);

                // Filtering to improve speed.
                // Only ~1ms, but we can have a million calls. 
                if (i <= 1000)
                {
                    var lineage2 = store.GetLineage();
                    Assert.False(lineage2.IsUncommitted(cb.Alias));
                }

                store.TryOpenBlock(blockId, out ub);
            }

            //  ~0.013ms to open & commit a new block against 500k blocks.
            _log.Log(LogSeverity.Info, $"{(double)watch.ElapsedMilliseconds / BlockHeight} ms per block (pure I/O).");

            watch.Reset();
            watch.Start();

            // ~70ms against 500k blocks with orphan at zero height (when disabling 'OldestOrphanHeight'). 
            var lastLineage = store.GetLineage();
            Assert.True(lastLineage.IsUncommitted(ub.Alias));

            _log.Log(LogSeverity.Info, $"{(double)watch.ElapsedMilliseconds } ms on GetLineage last block (at {BlockHeight} height).");
        }
    }
}
