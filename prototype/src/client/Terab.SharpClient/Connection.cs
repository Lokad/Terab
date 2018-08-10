using System;

namespace Terab.Client
{
    public class Connection : IDisposable
    {
        private readonly SafeConnectionHandle _connection;
        private readonly string _connectionString;

        internal Connection(string connectionString)
        {
            var status = PInvokes.terab_connect(connectionString, out _connection);
            if (PInvokes.ReturnCode.SUCCESS != status)
            {
                throw new Exception("invalid connection");
            }
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public BlockHandle OpenBlock(BlockHandle parent, out BlockUcid ucid)
        {
            var returnCode = PInvokes.terab_utxo_open_block(_connection, 0, out int handleAsInt, out PInvokes.BlockUcid blockUcid);
            if (returnCode != PInvokes.ReturnCode.SUCCESS)
            {
                throw new Exception(returnCode.ToString());
            }

            var ucidBytes = blockUcid.value;
            var left = BitConverter.ToUInt64(ucidBytes, 0);
            var right = BitConverter.ToUInt64(ucidBytes, 8);
            ucid = new BlockUcid(left, right);
            return new BlockHandle(handleAsInt);
        }

        public Block UtxoGetBlock(byte[ /*32*/] blockId)
        {
            if (blockId.Length != 32)
            {
                throw new ArgumentException("must have 32 bytes", nameof(blockId));
            }

            PInvokes.terab_utxo_get_block(_connection, blockId, out int blockHandle);
            return new Block(this, blockHandle);
        }
        public Block UtxoGetUncommittedBlock(BlockUcid blockUcid) => throw new NotImplementedException();
    }
}