// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;

namespace Terab.Lib.Coins
{
    /// <summary>
    /// Read/Write 'CoinPack' from a storage device. The writes are
    /// expected to be journalized to prevent data corruption if the
    /// call is interrupted half-way through the write operation.
    /// </summary>
    public interface IPackStore : IDisposable
    {
        /// <summary> The number of layers within the store. </summary>
        int LayerCount { get; }

        /// <summary> Every layer is assumed to have the same number of sectors.</summary>
        int SectorCount { get; }

        /// <summary> Sector sizes for each layer. The list count gives the number of layers. </summary>
        IReadOnlyList<int> SectorSizeInBytes { get; }

        /// <summary> To be called once, prior to 'Read()' or 'Write()'. </summary>
        void Initialize();

        CoinPack Read(int layerIndex, uint sectorIndex);

        /// <summary> Journalized write. </summary>
        void Write(ShortCoinPackCollection coll);
    }

    /// <summary>Contains at most 4 'TxoPack'.</summary>
    /// <remarks>
    /// As 'TxoPack' is a ref struct, it is not possible to have
    /// a regular collection holding the list of 'TxoPack's. However,
    /// as the maximal transaction size for 'TxoPack' is equal to the
    /// number of layers in the Sozu table, a simple work-around is
    /// possible for a collection of very limited capacity.
    /// </remarks>
    public ref struct ShortCoinPackCollection
    {
        private const int Capacity = 3;

        private int _count;

        private CoinPack _pack0;
        private CoinPack _pack1;
        private CoinPack _pack2;
        private CoinPack _pack3;

        public int Count => _count;

        public ShortCoinPackCollection(CoinPack pack0)
        {
            _pack0 = pack0;
            _pack1 = CoinPack.Empty;
            _pack2 = CoinPack.Empty;
            _pack3 = CoinPack.Empty;
            _count = 1;
        }

        public CoinPack this[int index]
        {
            get
            {
                if (index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                switch (index)
                {
                    case 0: return _pack0;
                    case 1: return _pack1;
                    case 2: return _pack2;
                    case 3: return _pack3;
                    default: throw new NotSupportedException();
                }
            }
            set
            {
                if (index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                switch (index)
                {
                    case 0: _pack0 = value; break;
                    case 1: _pack1 = value; break;
                    case 2: _pack2 = value; break;
                    case 3: _pack3 = value; break;
                    default: throw new NotSupportedException();
                }
            }
        }

        public void Add(CoinPack pack)
        {
            if (_count > Capacity)
                throw new InvalidOperationException("Exceeded capacity");

            switch (_count)
            {
                case 0:
                    _pack0 = pack;
                    break;
                case 1:
                    _pack1 = pack;
                    break;
                case 2:
                    _pack2 = pack;
                    break;
                case 3:
                    _pack3 = pack;
                    break;
            }

            _count++;
        }

        public void Reset()
        {
            _count = 0;
        }
    }
}