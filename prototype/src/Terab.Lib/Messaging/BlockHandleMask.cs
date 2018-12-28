// Copyright Lokad 2018 under MIT BCH.
namespace Terab.Lib.Messaging
{
    /// <summary>
    /// Used to convert BlockHandle into BlockAlias (and vice-versa).
    /// </summary>
    /// <remarks>
    /// XOR intends to opacify BlockAlias to BlockHandle, which prevents client
    /// to perform simple arithmetic on BlockAlias.
    /// </remarks>
    public struct BlockHandleMask
    {
        private readonly uint _value;

        public uint Value => _value;

        public BlockHandleMask(uint value)
        {
            _value = value;
        }
    }
}