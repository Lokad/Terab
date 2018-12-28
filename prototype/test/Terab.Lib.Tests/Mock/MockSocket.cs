// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Terab.Lib.Networking;
using Xunit;

namespace Terab.Lib.Tests.Mock
{
    [DebuggerDisplay("MockSocket (expect {NextMethod.Called})")]
    public class MockSocket : ISocketLike
    {
        public int Available
        {
            get
            {
                var next = _expected.Dequeue();
                CheckExpectedCall(next, Method.Available);
                var call = (Func<int>) next.Action;
                return call();
            }
        }

        public void Close()
        {
            var next = _expected.Dequeue();
            CheckExpectedCall(next, Method.Close);
        }

        public bool Connected
        {
            get
            {
                var next = _expected.Dequeue();
                CheckExpectedCall(next, Method.Connected);
                var call = (Func<bool>) next.Action;
                return call();
            }
        }

        public void Receive(Span<byte> bufferIn)
        {
            var next = _expected.Dequeue();
            CheckExpectedCall(next, Method.Receive);
            var call = (SpanToInt) next.Action;
            call(bufferIn);
        }

        public void Send(Span<byte> bufferOut)
        {
            var next = _expected.Dequeue();
            CheckExpectedCall(next, Method.Send);
            var call = (SpanToInt) next.Action;
            call(bufferOut);
        }

        private void CheckExpectedCall(Expected expected, Method actualCalled)
        {
            Assert.True(expected.Called == actualCalled,
                $"'{actualCalled}' called" + Environment.NewLine +
                $"   Instead, a call to '{expected.Called}' has been setup at {expected.Source}:{expected.Line}");
        }

        public void Expect(Method method, Delegate d, string fileName, int lineno)
        {
            _expected.Enqueue(new Expected {Called = method, Action = d, Source = fileName, Line = lineno});
        }

        public delegate int SpanToInt(Span<byte> bytes);

        public void ExpectSend(SpanToInt f, [CallerFilePath] string fileName = null,
            [CallerLineNumber] int lineno = 0) => Expect(Method.Send, f, fileName, lineno);

        public void ExpectReceive(SpanToInt f, [CallerFilePath] string fileName = null,
            [CallerLineNumber] int lineno = 0) => Expect(Method.Receive, f, fileName, lineno);

        public void ExpectAvailable(Func<int> f, [CallerFilePath] string fileName = null,
            [CallerLineNumber] int lineno = 0) => Expect(Method.Available, f, fileName, lineno);

        public void ExpectConnected(Func<bool> f, [CallerFilePath] string fileName = null,
            [CallerLineNumber] int lineno = 0) => Expect(Method.Connected, f, fileName, lineno);

        public void ExpectClose([CallerFilePath] string fileName = null, [CallerLineNumber] int lineno = 0) =>
            Expect(Method.Close, null, fileName, lineno);

        public void ExpectAllDone()
        {
            if (_expected.Count > 0)
            {
                foreach (var e in _expected)
                {
                    Console.WriteLine(e.Called);
                }
            }

            Assert.Equal(0, _expected.Count);
        }

        private readonly Queue<Expected> _expected = new Queue<Expected>();

        private Expected NextMethod => _expected.TryPeek(out var result) ? result : null;

        private class Expected
        {
            public Method Called { get; set; }
            public Delegate Action { get; set; }
            public string Source { get; set; }
            public int Line { get; set; }
        }

        public enum Method
        {
            Available,
            Close,
            Connected,
            Receive,
            Send
        }
    }
}