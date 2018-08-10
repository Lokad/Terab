using System;
using System.IO;
using Terab.UTXO.Core.Hash;
using Terab.UTXO.Core.Sozu;
using Xunit;

namespace Terab.UTXO.Core.Tests
{
    public class SozuTests
    {
        private static SozuTable PrepareTests(int layersCount, bool firstLayerVolatile = false)
        {

            var startHash = new Hash256(0xC000000000000000UL);
            var endHash = new Hash256(0xF400000000000000UL);

            var sozuFactoryConfigs = new SozuFactoryConfig[layersCount];
            sozuFactoryConfigs[0] = new SozuFactoryConfig(
                    2, 
                    firstLayerVolatile ? SozuFactory.VolatileStoreType : SozuFactory.TwoPhaseStoreType, 
                    4096);
    
            if (layersCount == 2)
            {
                sozuFactoryConfigs[1] = new SozuFactoryConfig(4, SozuFactory.TwoPhaseStoreType, 4096);
            }

            var path = String.Format(@".\Sozu{1}-{0}-{{0}}-Test.store", layersCount, firstLayerVolatile ? "Volatile" : "");
            var sf = new SozuFactory(null, startHash, endHash, sozuFactoryConfigs, path);

            var path1 = String.Format(path, 0);
            File.Delete(path1);
            File.Delete(Path.Combine(Path.GetDirectoryName(path1), @"hot-" + Path.GetFileName(path1)));
            if (layersCount == 2)
            {
                path1 = String.Format(path, 1);
                File.Delete(path1);
                File.Delete(Path.Combine(Path.GetDirectoryName(path1), @"hot-" + Path.GetFileName(path1)));
            }
            return sf.Create();
        }

        [Fact]
        public void Read()
        {
            var path = @"Read-Test.store";
            File.Delete(path);
            File.Delete(Path.Combine(Path.GetDirectoryName(path), @"hot-" + Path.GetFileName(path)));
            using (var fw = File.OpenWrite(path))
            {
                fw.Write(new byte[]
                {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                    0x01, 0x00, 0x00, 0x00, 0xf1, 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x22, 0x00, 0x00, 0x00, 0x00,
                    0x22, 0x22, 0x22, 0x22, 0x00, 0x00, 0x00, 0x00, 0x33, 0x33, 0x33, 0x33, 0x00, 0x00, 0x00, 0x00,
                    0x44, 0x44, 0x44, 0x44, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xa0, 0xb8, 0x02, 0xb4, 0xb6, 0x09, 0x57, 0x75,
                    0x10, 0x92, 0xd2, 0x04, 0x1b, 0xac, 0xae, 0x6e, 0x58, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xd6, 0x17, 0xe6, 0x61, 0x8f, 0xa0, 0xb9, 0x49, 0x99, 0xf9, 0xb2, 0x89, 0xe1, 0xb1, 0x72, 0xac,
                    0xb0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5a, 0x92, 0x65, 0xbe, 0xf7, 0x46, 0xc3, 0x12,
                    0xd1, 0xa2, 0x27, 0xfa, 0x1b, 0x2f, 0x3e, 0x4c, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xda, 0x26, 0x9a, 0x14, 0x9f, 0x07, 0xc9, 0xa8, 0x7d, 0xeb, 0xd7, 0xf7, 0x4f, 0x94, 0x9b, 0x7e
                });
            }
            var startHash = new Hash256(0xC000000000000000UL);
            var endHash = new Hash256(0xF400000000000000UL);

            var sozuFactoryConfigs = new SozuFactoryConfig[1];
            sozuFactoryConfigs[0] = new SozuFactoryConfig(2, SozuFactory.TwoPhaseStoreType, 88);

            var whatToWrite = new TxoPack(new Outpoint[1], new byte[2048]);
            var firstHash = new TxId(new Hash256(0xF111111111111122UL, 0x22222222UL, 0x33333333UL, 0x44444444UL));
            var wa = whatToWrite.GetResetWriter();
            var outpoint = new Outpoint(firstHash, 0, 8);
            var payload = new byte[8];
            payload[0] = 42;
            wa.Write(in outpoint, payload);

            var txoRead = new TxoPack(new Outpoint[1], new byte[2048]);

            var sozuTable = new SozuFactory(null, startHash, endHash, sozuFactoryConfigs, path).Create();
            sozuTable.Read(whatToWrite, txoRead);

            Assert.Equal(txoRead.Count, whatToWrite.Count);
            Assert.True(txoRead.OutPoints[0].Equals(whatToWrite.OutPoints[0]));
            Assert.True(payload.AsSpan().SequenceEqual(txoRead.Payloads.Slice(0, 8)));
        }

        [Fact]
        public void Write()
        {
            var sozuTable = PrepareTests(1);
            var whatToWrite = new TxoPack(new Outpoint[1], new byte[2048]);
            var firstHash = new TxId(new Hash256(0xF111111111111122UL, 0x22222222UL, 0x33333333UL, 0x44444444UL));
            var wa = whatToWrite.GetResetWriter();
            var outpoint = new Outpoint(firstHash, 0, 100);
            var payload = new byte[100];
            payload[0] = 42;
            wa.Write(in outpoint, payload);

            sozuTable.Write(whatToWrite);
            var txoRead = new TxoPack(new Outpoint[1], new byte[2048]);
            
            sozuTable.Read(whatToWrite, txoRead);
            Assert.Equal(txoRead.Count, whatToWrite.Count);
            Assert.True(txoRead.OutPoints[0].Equals(whatToWrite.OutPoints[0]));
            Assert.True(txoRead.Payloads.SequenceEqual(whatToWrite.Payloads));
        }

        [Fact]
        public void OverFlow()
        {
            var sozuTable = PrepareTests(2);
            var whatToWrite = new TxoPack(new Outpoint[120], new byte[8192]);
            var wa = whatToWrite.GetResetWriter();
            for (var i = 0UL; i < 20; i++)
            {
                var txid = new TxId(new Hash256(0xF111111111111122UL, 0x22222222UL, 0x33333333UL, 0x44444444UL + i));
                var outpoint = new Outpoint(txid, 0, 200);
                var payload = new byte[200];
                payload[8] = 8;
                payload[16+i] = 42;
                wa.Write(in outpoint, payload);
            }
            sozuTable.Write(whatToWrite);

            var txoRead = new TxoPack(new Outpoint[120], new byte[8192]);
            sozuTable.Read(whatToWrite, txoRead);
            Assert.Equal(txoRead.Count, whatToWrite.Count);
            for (var i = 0; i < txoRead.Count; ++i)
            {
                Assert.True(txoRead.OutPoints[i].Equals(whatToWrite.OutPoints[i]));
                Assert.True(txoRead.Payloads.SequenceEqual(whatToWrite.Payloads));
            }
        }

        [Fact]
        public void WriteVolatile()
        {
            var sozuTable = PrepareTests(1, /*firstLayerVolatile*/ true);
            var whatToWrite = new TxoPack(new Outpoint[1], new byte[2048]);
            var firstHash = new TxId(new Hash256(0xF111111111111122UL, 0x22222222UL, 0x33333333UL, 0x44444444UL));
            var wa = whatToWrite.GetResetWriter();
            var outpoint = new Outpoint(firstHash, 0, 100);
            var payload = new byte[100];
            payload[0] = 42;
            wa.Write(in outpoint, payload);

            sozuTable.Write(whatToWrite);
            var txoRead = new TxoPack(new Outpoint[1], new byte[2048]);

            sozuTable.Read(whatToWrite, txoRead);
            Assert.Equal(txoRead.Count, whatToWrite.Count);
            Assert.True(txoRead.OutPoints[0].Equals(whatToWrite.OutPoints[0]));
            Assert.True(txoRead.Payloads.SequenceEqual(whatToWrite.Payloads));
        }

        [Fact]
        public void OverFlowVolatile()
        {
            var sozuTable = PrepareTests(2, /*firstLayerVolatile*/ true);
            var whatToWrite = new TxoPack(new Outpoint[120], new byte[8192]);
            var wa = whatToWrite.GetResetWriter();
            for (var i = 0UL; i < 20; i++)
            {
                var txid = new TxId(new Hash256(0xF111111111111122UL, 0x22222222UL, 0x33333333UL, 0x44444444UL + i));
                var outpoint = new Outpoint(txid, 0, 200);
                var payload = new byte[200];
                payload[8] = 8;
                payload[16 + i] = 42;
                wa.Write(in outpoint, payload);
            }
            sozuTable.Write(whatToWrite);

            var txoRead = new TxoPack(new Outpoint[120], new byte[8192]);
            sozuTable.Read(whatToWrite, txoRead);
            Assert.Equal(txoRead.Count, whatToWrite.Count);
            for (var i = 0; i < txoRead.Count; ++i)
            {
                Assert.True(txoRead.OutPoints[i].Equals(whatToWrite.OutPoints[i]));
                Assert.True(txoRead.Payloads.SequenceEqual(whatToWrite.Payloads));
            }
        }

    }
}
