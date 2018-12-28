// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib.Coins.Exceptions
{
    public abstract class BaseSozuException : ApplicationException
    {
        private protected BaseSozuException(string message) : base(message)
        {
        }

        private protected BaseSozuException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}