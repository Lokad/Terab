namespace Terab.Client
{
    public class Block // can be uncommitted, or committed.
    {
        internal Block(Connection connection, int handleValue)
        {
            _connection = connection;
            this.Handle = new BlockHandle(handleValue);
        }
        private readonly Connection _connection;
        public readonly BlockHandle Handle;

        //public BlockInfo GetBlockInfo() => throw new NotImplementedException();
        //
        //// creates a child block of the current (this) block. Until it is, in turn, committed, it is an uncommitted block,
        //// and is meant to serve as "draft block" for pushing transactions still "in the making"
        //public Block OpenBlock() => throw new NotImplementedException();
        //public Block OpenBlock(out BlockUcid ucid) => throw new NotImplementedException();
        //
        //public void WriteTxs(Txo[] txos) => throw new NotImplementedException();
        //public void UtxoGet(TxOutpoint[] outpoints, Txo[] txos, byte[] storage) => throw new NotImplementedException();
        //public void Commit(byte[32] blockId) => throw new NotImplementedException();
    }
}