// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging.NativeClient
{
    [Flags]
    public enum BlockFlags : uint
    {
        Frozen = 0x01,
        Committed = 0x02,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BlockInfo
    {
        public BlockHandle Parent;
        public BlockFlags Flags;
        public int BlockHeight;
        public CommittedBlockId BlockId;
    }
}
