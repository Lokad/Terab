// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging.NativeClient
{
    [Flags]
    public enum CoinFlags : byte
    {
        None = 0,
        Coinbase = 1,
    }

    [Flags]
    public enum CoinStatus : byte
    {
        None = 0,
        Success = 1 << 0,
        OutpointNotFound = 1 << 1,
        InvalidContext = 1 << 2,
        InvalidBlockHandle = 1 << 3,
        StorageTooShort = 1 << 4,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Coin
    {
        public Outpoint Outpoint;
        public BlockHandle Production;
        public BlockHandle Consumption;
        public ulong Satoshis;
        public uint NLockTime;
        public int ScriptOffset;
        public int ScriptLength;
        public CoinFlags Flags;
        public CoinStatus Status;
    }
}