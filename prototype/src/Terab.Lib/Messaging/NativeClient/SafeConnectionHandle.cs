// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Runtime.ConstrainedExecution;

namespace Terab.Lib.Messaging.NativeClient
{
    internal class SafeConnectionHandle :
        Microsoft.Win32.SafeHandles.CriticalHandleZeroOrMinusOneIsInvalid
    {
        string _reason = "unknown";

        public SafeConnectionHandle(IntPtr handle)
        {
            SetHandle(handle);
        }

        public SafeConnectionHandle()
        {
        }

        protected override void Dispose(bool disposing)
        {
            _reason = disposing ? "disposing" : "finalization";
            base.Dispose(disposing);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            var returnCode = PInvokes.terab_disconnect(this, _reason);
            return returnCode == ReturnCode.SUCCESS;
        }
    }
}