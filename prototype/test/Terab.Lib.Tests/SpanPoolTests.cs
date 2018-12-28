// Copyright Lokad 2018 under MIT BCH.
using System;
using Terab.Lib.Messaging;
using Xunit;

namespace Terab.Lib.Tests
{
    public class SpanPoolTests
    {
        [Fact]
        public void GetSpans()
        {
            var pool = new SpanPool<RequestId>(20);
            var firstPart = pool.GetSpan(3);
            var secondPart = pool.GetSpan(17);

            firstPart[0] = new RequestId();
            firstPart[2] = new RequestId();

            Assert.Equal(default, secondPart[0]);
            secondPart[0] = new RequestId(3);
            Assert.Equal(new RequestId(3), secondPart[0]);

            secondPart[16] = new RequestId(4);
            Assert.Equal(new RequestId(4), secondPart[16]);

            Assert.Equal(default, firstPart[1]);
            Assert.Equal(default, secondPart[10]);
            Assert.Equal(default, secondPart[13]);
        }

        [Fact]
        public void TryGetBeyondCapacity()
        {
            var pool = new SpanPool<RequestId>(20);
            pool.GetSpan(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.GetSpan(18));
        }

        [Fact]
        public void TestReset()
        {
            var pool = new SpanPool<RequestId>(20);
            var firstPart = pool.GetSpan(3);
            var secondPart = pool.GetSpan(17);

            firstPart[2] = new RequestId(10);
            secondPart[10] = new RequestId(3);

            pool.Reset();

            var secondFirstPart = pool.GetSpan(10);
            Assert.Equal(default, secondFirstPart[2]);
            var secondSecondPart = pool.GetSpan(10);
            Assert.Equal(default, secondSecondPart[3]);
        }
    }
}