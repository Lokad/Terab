// Copyright Lokad 2018 under MIT BCH.
namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Canonical outpoint flags in Bitcoin.
    /// </summary>
    public enum OutpointFlags : byte
    {
        None = 0,
        IsCoinbase = 1,
    }
}