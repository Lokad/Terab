using System;
using System.Runtime.Serialization;

namespace Terab.UTXO.Core.Sozu
{
    public class SozuTableOverflowException : Exception
    {
        public SozuTableOverflowException() : base()
        {
        }

        public SozuTableOverflowException(string message) : base(message)
        {
        }

        public SozuTableOverflowException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SozuTableOverflowException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}