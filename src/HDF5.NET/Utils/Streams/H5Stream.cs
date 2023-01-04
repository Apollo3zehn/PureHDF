using Microsoft.Win32.SafeHandles;

namespace HDF5.NET
{
    internal abstract class H5Stream : Stream
    {
        public H5Stream(bool isStackOnly, SafeFileHandle? safeFileHandle)
        {
            IsStackOnly = isStackOnly;
            SafeFileHandle = safeFileHandle;
        }

        public bool IsStackOnly { get; }
        public SafeFileHandle? SafeFileHandle { get; }
    }
}
