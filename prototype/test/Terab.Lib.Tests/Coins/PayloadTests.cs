// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Linq;
using Terab.Lib.Coins;
using Xunit;

namespace Terab.Lib.Tests.Coins
{
    public class PayloadTests
    {
        [Fact]
        public void GetSetSatoshis()
        {
            var payload = new Payload(new byte[4096]);
            Assert.Equal(0ul, payload.Satoshis);

            payload.Satoshis = 42ul;
            Assert.Equal(42ul, payload.Satoshis);
        }

        [Fact]
        public void GetSetNLockTime()
        {
            var payload = new Payload(new byte[4096]);
            Assert.Equal(0u, payload.NLockTime);

            payload.NLockTime = 42;
            Assert.Equal(42u, payload.NLockTime);
        }

        [Fact]
        public void GetSetScript()
        {
            var payload = new Payload(new byte[4096]);

            var script = new byte[123];
            for (var i = 0; i < script.Length; i++)
                script[i] = (byte) i;

            payload.Append(script);
            Assert.True(script.SequenceEqual(payload.Script.ToArray()));
        }

        [Fact]
        public void GetSetSpan()
        {
            var pl1 = new Payload(new byte[4096]);
            pl1.Satoshis = 42ul;
            pl1.NLockTime = 24;

            var script = new byte[123];
            for (var i = 0; i < script.Length; i++)
                script[i] = (byte)i;

            pl1.Append(script);

            var pl2 = new Payload(pl1.Span);

            Assert.Equal(pl1.Satoshis, pl2.Satoshis);
            Assert.Equal(pl1.NLockTime, pl2.NLockTime);
            Assert.True(pl1.Script.SequenceEqual(pl2.Script.ToArray()));
        }
    }
}
