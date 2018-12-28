// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Terab.Lib.Coins;

namespace Terab.Lib.Tests.Mock
{
    /// <summary>
    /// Memory-only mock storage implementation, intended for tests and benchmarks.
    /// </summary>
    public class VolatilePackStore : IPackStore
    {
        private const int InitialBottomLayerSectorSize = 512;

        private readonly int _sectorCount;

        private readonly int[] _sectorSizeInBytes;

        private readonly Memory<byte>[] _layers;

        private readonly IKeyValueStore<uint> _bottomLayer;

        public int LayerCount => _sectorSizeInBytes.Length + 1;
        public int SectorCount => _sectorCount;
        public IReadOnlyList<int> SectorSizeInBytes => _sectorSizeInBytes;

        public VolatilePackStore(int sectorCount, int[] sectorSizeInBytes)
        {
            _sectorSizeInBytes = sectorSizeInBytes;
            _sectorCount = sectorCount;
            _layers = new Memory<byte>[_sectorSizeInBytes.Length];
            _bottomLayer = new VolatileKeyValueStore<uint>();

            for (var i = 0; i < sectorSizeInBytes.Length; ++i)
            {
                long length = _sectorSizeInBytes[i] * _sectorCount;
                if (length > Int32.MaxValue)
                {
                    throw new ArgumentOutOfRangeException
                        ($"{nameof(VolatilePackStore)} length cannot exceed {Int32.MaxValue}");
                }

                _layers[i] = new Memory<byte>(new byte[(int) length]);
            }
        }

        /// <summary> Nothing done. </summary>
        public void Initialize() { }

        private Span<byte> ReadSpan(int layerIndex, uint sectorIndex)
        {
            if (layerIndex >= _layers.Length)
            {
                if (!_bottomLayer.TryGet(sectorIndex, out var span))
                {
                    // In case where key sectorIndex doesn't exist,
                    // we initialize a empty array as value for this key.
                    // We can see this as lazy sector allocation (only
                    // for the bottom layer).

                    Span<byte> mem = stackalloc byte[InitialBottomLayerSectorSize];
                    var pack = new CoinPack(mem) { SectorIndex = sectorIndex, LayerIndex = (byte)layerIndex };

                    _bottomLayer.Set(sectorIndex, mem);
                    _bottomLayer.TryGet(sectorIndex, out span);
                }
                return span;
            }
            else
            {
                return _layers[layerIndex].Slice((int)(sectorIndex * _sectorSizeInBytes[layerIndex]),
                    _sectorSizeInBytes[layerIndex]).Span;
            }
        }

        public CoinPack Read(int layerIndex, uint sectorIndex)
        {
            var span = ReadSpan(layerIndex, sectorIndex);
            var pack = new CoinPack(span);

            // storage pack header not initialized, we initialize it:
            if (pack.SectorIndex == 0 && pack.LayerIndex == 0)
            {
                pack.SectorIndex = sectorIndex;
                pack.LayerIndex = (byte)layerIndex;
            }

            if (pack.SectorIndex != sectorIndex)
                throw new InvalidOperationException("SectorIndex mismatch.");

            return pack;
        }

        /// <summary>
        /// Simply write all packs, no journal is required.
        /// </summary>
        public void Write(ShortCoinPackCollection coll)
        {
            for (var i = 0; i < coll.Count; ++i)
            {
                var pack = coll[i];

                Debug.Assert(pack.LayerIndex <= _layers.Length);
                if (pack.LayerIndex == _layers.Length)
                {
                    _bottomLayer.Set(pack.SectorIndex, pack.Span);
                }
                else
                {
                    pack.Span.CopyTo(ReadSpan(pack.LayerIndex, pack.SectorIndex));
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
