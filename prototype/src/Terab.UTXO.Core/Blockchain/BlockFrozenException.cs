using System;

namespace Terab.UTXO.Core.Blockchain
{
    // TODO: [vermorel] This exception should be completed or removed.

    /// <summary>
    /// Exception that is thrown when someone tries
    /// to add a child to a block that is below the
    /// frozen limit and can therefore not have any
    /// additional children anymore.
    /// </summary>
    public class BlockFrozenException : Exception
    {
        //public CommittedBlock Head { get; }

        //public CommittedBlock Parent { get; }

        //public BlockFrozenException(CommittedBlock head, CommittedBlock parent) : base(
        //    $"No child can be added to {parent.BlockId} as head is at {head.BlockId}.")
        //{
        //    Head = head;
        //    Parent = parent;
        //}
    }
}
