using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{Message.Datatype.Class}'")]
    public class H5Attribute : IH5DataContainer
    {
        #region Fields

        private Superblock _superblock;

        #endregion

        #region Constructors

        internal H5Attribute(AttributeMessage message, Superblock superblock)
        {
            this.Message = message;
            _superblock = superblock;
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

        public T[] ReadCompound<T>() where T : struct
        {
            return this.ReadCompound<T>(fieldInfo => fieldInfo.Name);
        }

        public unsafe T[] ReadCompound<T>(Func<FieldInfo, string> getName) where T : struct
        {
            return H5Utils.ReadCompound<T>(this.Message.Datatype, this.Message.Dataspace, _superblock, this.Message.Data, getName);
        }

        public string[] ReadString()
        {
            return H5Utils.ReadString(this.Message.Datatype, this.Message.Data, _superblock);
        }

        #endregion
    }
}
