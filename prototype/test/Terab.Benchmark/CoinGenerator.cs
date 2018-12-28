// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;

namespace Terab.Benchmark
{
    [DebuggerDisplay("{" + nameof(PrettyPrint) + "}")]
    public struct CoinSketch : IComparable<CoinSketch>
    {
        public float Time { get; set; }
        public uint CoinId { get; set; }

        public string PrettyPrint
        {
            get
            {
                if (IsProduction)
                {
                    return "Production(" + (CoinId >> 2) + ")";
                }

                if (IsConsumption)
                {
                    return "Consumption(" + (CoinId >> 2) + ")";
                }

                return "Read(" + (CoinId >> 2) + ")";
            }
        }

        public CoinSketch(float time, uint coinId)
        {
            Time = time;
            CoinId = coinId;
        }

        public bool IsProduction => CoinId % 4 == 0;

        public bool IsRead => CoinId % 4 == 1;

        public bool IsConsumption => CoinId % 4 == 2;

        public int CompareTo(CoinSketch other)
        {
            var timeComparison = Time.CompareTo(other.Time);
            if (timeComparison != 0) return timeComparison;
            return CoinId.CompareTo(other.CoinId);
        }
    }

    public class CoinGenerator
    {
        private readonly Random _rand;
        private CoinSketch[] _sketches;

        public Random Random => _rand;

        public CoinSketch[] CoinSketches => _sketches;

        public CoinGenerator(Random rand)
        {
            _rand = rand;
        }

        public void GenerateSketches(int count)
        {
            _sketches = new CoinSketch[count];

            var index = 0;
            var coinId = 0u;
            while(index < _sketches.Length)
            {
                // Production event
                var s0 = new CoinSketch((float)_rand.NextDouble(), coinId);
                _sketches[index++] = s0;

                if (index >= _sketches.Length)
                    break;

                // Read event
                var delta1 = 1f;
                for (var i = 0; i < 10; i++)
                    delta1 *= (float) _rand.NextDouble();

                var s1 = new CoinSketch(s0.Time + delta1, coinId + 1);

                if (s1.Time >= 1)
                    continue;

                _sketches[index++] = s1;

                if (index >= _sketches.Length)
                    break;

                // Consumption event
                var delta2 = 100f / count;

                var s2 = new CoinSketch(s1.Time + delta2, coinId + 2);

                if (s2.Time >= 1)
                    continue;

                _sketches[index++] = s2;

                coinId += 4;
            }

            Array.Sort(_sketches);
        }
    }
}
