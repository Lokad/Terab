// Copyright Lokad 2018 under MIT BCH.
using System;

namespace Terab.Lib.Messaging.NativeClient
{
    public class TerabClient : IDisposable
    {
        public const int MaxCoinsPerCall = 1000;

        private static readonly object InitializationRoot = new object();
        private static int _clientCount = 0;

        private readonly SafeConnectionHandle _connection;

        public TerabClient(string connectionString)
        {
            lock (InitializationRoot)
            {
                if (++_clientCount == 1)
                {
                    var err = PInvokes.terab_initialize();
                    if (err != ReturnCode.SUCCESS)
                    {
                        throw new TerabException("Terab initialization failed.", err);
                    }
                }
            }

            var status = PInvokes.terab_connect(connectionString, out _connection);

            if (ReturnCode.SUCCESS != status)
            {
                throw new TerabException("Invalid connection to a Terab instance.", status);
            }
        }

        public void Dispose()
        {
            _connection.Dispose();

            lock (InitializationRoot)
            {
                if (--_clientCount == 0)
                {
                    PInvokes.terab_shutdown();
                }
            }
        }

        public BlockHandle OpenBlock(CommittedBlockId parent, out UncommittedBlockId ucid)
        {
            ucid = default;

            var returnCode = PInvokes.terab_utxo_open_block(
                _connection,
                ref parent,
                out BlockHandle handle,
                ref ucid);

            if (returnCode != ReturnCode.SUCCESS)
            {
                throw new TerabException("OpenBlock failed.", returnCode);
            }

            return handle;
        }

        public void CommitBlock(BlockHandle handle, CommittedBlockId blockId)
        {
            var returnCode = PInvokes.terab_utxo_commit_block(_connection, handle, ref blockId);

            if (returnCode != ReturnCode.SUCCESS)
            {
                throw new TerabException("CommitBlock failed.", returnCode);
            }
        }

        public BlockHandle GetCommittedBlock(CommittedBlockId blockId)
        {
            var returnCode = PInvokes.terab_utxo_get_committed_block(
                _connection, ref blockId, out BlockHandle handle);

            if (returnCode != ReturnCode.SUCCESS)
            {
                throw new TerabException("GetCommittedBlock failed.", returnCode);
            }

            return handle;
        }

        public BlockHandle GetUncommittedBlock(UncommittedBlockId blockUcid)
        {
            var returnCode = PInvokes.terab_utxo_get_uncommitted_block(
                _connection, ref blockUcid, out BlockHandle handle);

            if (returnCode != ReturnCode.SUCCESS)
            {
                throw new TerabException("GetUncommittedBlock failed.", returnCode);
            }

            return handle;
        }

        public BlockInfo GetBlockInfo(BlockHandle handle)
        {
            var info = new BlockInfo();
            var returnCode = PInvokes.terab_utxo_get_blockinfo(
                _connection, handle, ref info);

            if (returnCode != ReturnCode.SUCCESS)
            {
                throw new TerabException("GetBlockInfo failed.", returnCode);
            }

            return info;
        }

        public unsafe void SetCoins(BlockHandle context, Span<Coin> coins, Span<byte> storage)
        {
            if (coins.Length > MaxCoinsPerCall)
            {
                throw new ArgumentOutOfRangeException(nameof(coins), "Too many coins.");
            }

            ref var coinRef = ref coins.GetPinnableReference();
            ref var storageRef = ref storage.GetPinnableReference();

            fixed (Coin* coinPtr = &coinRef)
            fixed (byte* storagePtr = &storageRef)
            {
                var returnCode = PInvokes.terab_utxo_set_coins(
                    _connection,
                    context,
                    coins.Length,
                    coinPtr,
                    storage.Length,
                    storagePtr);

                if (returnCode != ReturnCode.SUCCESS)
                {
                    throw new TerabException("SetCoins failed.", returnCode);
                }
            }
        }

        public unsafe void GetCoins(BlockHandle context, Span<Coin> coins, Span<byte> storage)
        {
            if (coins.Length > MaxCoinsPerCall)
            {
                throw new ArgumentOutOfRangeException(nameof(coins), "Too many coins.");
            }

            ref var coinRef = ref coins.GetPinnableReference();
            ref var storageRef = ref storage.GetPinnableReference();

            fixed (Coin* coinPtr = &coinRef)
            fixed (byte* storagePtr = &storageRef)
            {
                var returnCode = PInvokes.terab_utxo_get_coins(
                    _connection,
                    context,
                    coins.Length,
                    coinPtr,
                    storage.Length,
                    storagePtr);

                if (returnCode != ReturnCode.SUCCESS)
                {
                    throw new TerabException("GetCoins failed.", returnCode);
                }
            }
        }
    }
}