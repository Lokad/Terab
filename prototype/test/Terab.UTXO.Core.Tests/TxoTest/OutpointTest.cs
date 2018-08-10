using Terab.UTXO.Core.Hash;
using Xunit;

namespace Terab.UTXO.Core.Tests.TxoTest
{
    public class OutpointFixture
    {
        public ulong a1 => 0xBC311F1A50F3A2DD;
        public ulong a2 => 0xEF4420D1A6037CAD;
        public ulong a3 => 0xAEE316C262E8BF22;
        public ulong a4 => 0x6F356492D7983D84;

        public TxId SpecificTxId { get; }

        public TxId Zero { get; }

        public TxId NonZero { get; }

        public Outpoint Outpoint { get; }

        public Outpoint ZeroOutpoint { get; }

        public Outpoint NotZeroOutpoint { get; }

        public OutpointFixture()
        {
            SpecificTxId = new TxId(new Hash256(a1, a2, a3, a4));
            Zero = new TxId(new Hash256(0UL));
            NonZero = new TxId(new Hash256(~0UL, ~0UL, ~0UL, ~0UL));
            Outpoint = new Outpoint(SpecificTxId, 0, 512);
            ZeroOutpoint = new Outpoint(Zero, 0, 0);
            NotZeroOutpoint = new Outpoint(NonZero, ~0, ushort.MaxValue, OutpointFlags.None);
        }
    }

    public class OutpointTest : IClassFixture<OutpointFixture>
    {
        private Outpoint _outpointZero, _outpointNotZero;

        public OutpointTest(OutpointFixture fixture)
        {
            _outpointZero = fixture.ZeroOutpoint;
            _outpointNotZero = fixture.NotZeroOutpoint;
        }

        [Fact]
        public void Top()
        {
            var topZero = _outpointZero.Top(16);
            Assert.Equal(0, topZero);
            var topNotZero = _outpointNotZero.Top(16);
            Assert.Equal(0x0000FFFF, topNotZero);

            var topZero2 = _outpointZero.Top(1);
            Assert.Equal(0, topZero2);
            var topNotZero2 = _outpointNotZero.Top(1);
            Assert.Equal(0x00000001, topNotZero2);
        }
    }
}