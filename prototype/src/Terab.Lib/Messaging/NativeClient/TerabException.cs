// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib.Messaging.NativeClient
{
    public class TerabException : ApplicationException
    {
        public ReturnCode ReturnCode { get; private set; }

        public TerabException(string message, ReturnCode code) : base(message)
        {
            ReturnCode = code;
        }
    }
}