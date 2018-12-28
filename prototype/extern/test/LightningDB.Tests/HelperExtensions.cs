using System;
using System.Collections.Generic;
using System.Text;

namespace LightningDB.Tests
{
    public static class HelperExtensions
    {
        public static bool StartsWith(this byte[] source, byte[] prefix)
        {
            var length = prefix.Length;
            for (var i = 0; i < length; ++i)
            {
                if (source[i] == prefix[i])
                    continue;
                return false;
            }
            return length != 0;
        }
    }
}
