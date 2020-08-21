using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5Attribute
    {
        #region Constructors

        internal H5Attribute(AttributeMessage message)
        {
            this.Message = message;
        }

        #endregion

        #region Properties

        public AttributeMessage Message { get; }

        public string Name => this.Message.Name;

        #endregion

        #region Methods

        public T[] Read<T>() where T : unmanaged
        {
            return MemoryMarshal
                .Cast<byte, T>(this.Message.Data)
                .ToArray();
        }

        #endregion
    }
}
