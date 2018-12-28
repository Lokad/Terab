// Copyright Lokad 2018 under MIT BCH.
using System;
using Xunit.Abstractions;

namespace Terab.Lib.Tests.Mock
{
    public class XLog : ILog
    {
        private readonly ITestOutputHelper _output;

        public XLog(ITestOutputHelper output)
        {
            _output = output;
        }

        public void Log(LogSeverity severity, string message)
        {
            _output.WriteLine($"{DateTime.UtcNow:s}, {severity}: {message}");
        }
    }
}