// Copyright Lokad 2018 under MIT BCH.
using System.Runtime.InteropServices;

namespace Terab.Lib.Messaging.NativeClient
{
    internal static partial class PInvokes
    {
        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_initialize();

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_shutdown();

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_connect([MarshalAs(UnmanagedType.LPStr)] string cnxString,
            out SafeConnectionHandle connectionHandle);

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_disconnect(SafeConnectionHandle connectionHandle,
            [MarshalAs(UnmanagedType.LPStr)] string reason);


        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_utxo_open_block(
            SafeConnectionHandle connection,
            ref CommittedBlockId parentId,
            out BlockHandle handle,
            ref UncommittedBlockId ucid);

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_utxo_commit_block(
            SafeConnectionHandle connection,
            BlockHandle handle,
            ref CommittedBlockId blockId);

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_utxo_get_committed_block(
            SafeConnectionHandle connection,
            ref CommittedBlockId blockId,
            out BlockHandle handle);

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_utxo_get_uncommitted_block(
            SafeConnectionHandle connection,
            ref UncommittedBlockId blockUcid,
            out BlockHandle handle);

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern ReturnCode terab_utxo_get_blockinfo(
            SafeConnectionHandle connection,
            BlockHandle handle,
            ref BlockInfo info);

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe ReturnCode terab_utxo_get_coins(
            SafeConnectionHandle connection,
            BlockHandle context,
            int coinLength,
            Coin* coins,
            int storageLength,
            byte* storage
        );

        [DllImport("terabclient", CallingConvention = CallingConvention.Cdecl)]
        internal static extern unsafe ReturnCode terab_utxo_set_coins(
            SafeConnectionHandle connection,
            BlockHandle context,
            int coinLength,
            Coin* coins,
            int storageLength,
            byte* storage
        );
    }
}