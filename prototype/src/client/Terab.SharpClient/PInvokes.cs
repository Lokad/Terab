using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;

namespace Terab.Client
{
    internal class SafeConnectionHandle :  Microsoft.Win32.SafeHandles.CriticalHandleZeroOrMinusOneIsInvalid
    {
        string reason = "unknown";

        public SafeConnectionHandle(IntPtr handle)
        {
            SetHandle(handle);
        }
        public SafeConnectionHandle() {}

        protected override void Dispose(bool disposing)
        {
            reason = disposing ? "disposing" : "finalization";
            base.Dispose(disposing);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            PInvokes.terab_disconnect(this, reason);
            return true; // TODO: it's probably not worth crashing the process because 
                         // a (client) socket instance was not properly closed, but maybe
                         // we should report the issue somewhere
        }
    }

    internal static class PInvokes
    {
        internal enum ReturnCode : Int32
        {
            SUCCESS                   =  0, /* Successful call. */
            ERR_CONNECTION_FAILED     =  1, /* Failed to connect to the Terab service. */
            ERR_TOO_MANY_CLIENTS      =  2, /* Connection rejected, too many clients. */
            ERR_AUTHENTICATION_FAILED =  3, /* Failed to authenticate with the Terab service. */
            ERR_SERVICE_UNAVAILABLE   =  4, /* Terab service is not ready yet to accept requests. */
            ERR_TOO_MANY_REQUESTS     =  5, /* Too many requests are concurrently made to Terab. */
            ERR_INTERNAL_ERROR        =  6, /* Something wrong happened. Contact the Terab team. */
            ERR_STORAGE_FULL          =  7, /* No more storage left for the write operation. */
            ERR_STORAGE_CORRUPTED     =  8, /* Non-recoverable data corruption at the service level. */
            ERR_BLOCK_CORRUPTED       =  9, /* The block being written is corrupted and cannot be recovered. */
            ERR_BLOCK_FROZEN          = 10, /* This block is too old and does not accept new children blocks. */
            ERR_BLOCK_COMMITTED       = 11, /* This block is committed and does not accept new txs. */
            ERR_BLOCK_UNCOMMITTED     = 12, /* This block is not committed and does not accept children blocks. */
            ERR_BLOCK_UNKNOWN         = 13, /* A block handle refers to an unknown block. */
            ERR_INCONSISTENT_REQUEST  = 14, /* Broken idempotence. Request contradicts previous one.*/
            ERR_INVALID_REQUEST       = 15, /* Generic invalidity of the arguments of the request. */
        }

        [DllImport(dllName: "Terab.BaseClient.dll", CallingConvention = CallingConvention.Cdecl)]
        internal extern static ReturnCode terab_initialize();

        [DllImport(dllName: "Terab.BaseClient.dll", CallingConvention = CallingConvention.Cdecl)]
        internal extern static ReturnCode terab_shutdown();

        [DllImport(dllName: "Terab.BaseClient.dll", CallingConvention = CallingConvention.Cdecl)]
        internal extern static ReturnCode terab_connect( [MarshalAs(UnmanagedType.LPStr)] string cnxString, out SafeConnectionHandle connectionHandle);

        //[DllImport(dllName: "Terab.BaseClientVs.dll", CallingConvention = CallingConvention.Cdecl)]
        //internal extern static SafeConnectionHandle terab_connect2( [MarshalAs(UnmanagedType.LPStr)] string cnxString);

        [DllImport(dllName: "Terab.BaseClient.dll", CallingConvention = CallingConvention.Cdecl)]
        internal extern static ReturnCode terab_disconnect( SafeConnectionHandle connectionHandle, [MarshalAs(UnmanagedType.LPStr)] string reason);

        [DllImport(dllName: "Terab.BaseClient.dll", CallingConvention = CallingConvention.Cdecl)]
        internal extern static ReturnCode terab_utxo_get_block( SafeConnectionHandle connection, byte[] blockId, out Int32 blockHandle);

        internal struct BlockUcid
        {
            // TODO this is as expansive as PInvoke can get (when marshalling 16 bytes), so either:
            // TODO   we go "full hack" (https://stackoverflow.com/questions/10320502/c-sharp-calling-c-function-that-returns-struct-with-fixed-size-char-array/10320690 , JaredPar answer),
            // TODO   we decide we don't care
            // TODO   we change the definition of block_ucid in the public API
            // TODO   oe we write a small C helper, living in a lib dedicated to easing the P/Invoke wrapping
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] value;
        }

        [DllImport(dllName: "Terab.BaseClient.dll", CallingConvention = CallingConvention.Cdecl)]
        internal extern static ReturnCode terab_utxo_open_block( SafeConnectionHandle connection, Int32 parentHandle, out Int32 blockHandle, out BlockUcid ucid);
    }
}
