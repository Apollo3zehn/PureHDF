using Microsoft.Win32.SafeHandles;

namespace HDF5.NET
{
    internal abstract class H5Stream : Stream
    {
        public H5Stream(bool isStackOnly, SafeFileHandle? safeFileHandle, long safeFileHandleOffset = 0)
        {
            IsStackOnly = isStackOnly;
            SafeFileHandle = safeFileHandle;
            SafeFileHandleOffset = safeFileHandleOffset;
        }

        public bool IsStackOnly { get; }
        public SafeFileHandle? SafeFileHandle { get; }
        public long SafeFileHandleOffset { get; }
    }
}
