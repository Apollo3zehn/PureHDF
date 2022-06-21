using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    public partial class H5Attribute
    {
        #region Properties

        public string Name => Message.Name;

        public H5Dataspace Space
        {
            get
            {
                if (_space is null)
                    _space = new H5Dataspace(Message.Dataspace);

                return _space;
            }
        }

        public H5DataType Type
        {
            get
            {
                if (_type is null)
                    _type = new H5DataType(Message.Datatype);

                return _type;
            }
        }

        #endregion

        #region Methods

        public T[] Read<T>()
            where T : unmanaged
        {
            switch (Message.Datatype.Class)
            {
                case DatatypeMessageClass.FixedPoint:
                case DatatypeMessageClass.FloatingPoint:
                case DatatypeMessageClass.BitField:
                case DatatypeMessageClass.Opaque:
                case DatatypeMessageClass.Compound:
                case DatatypeMessageClass.Reference:
                case DatatypeMessageClass.Enumerated:
                case DatatypeMessageClass.Array:
                    break;

                default:
                    throw new Exception($"This method can only be used with one of the following type classes: '{DatatypeMessageClass.FixedPoint}', '{DatatypeMessageClass.FloatingPoint}', '{DatatypeMessageClass.BitField}', '{DatatypeMessageClass.Opaque}', '{DatatypeMessageClass.Compound}', '{DatatypeMessageClass.Reference}', '{DatatypeMessageClass.Enumerated}' and '{DatatypeMessageClass.Array}'.");
            }

            var buffer = Message.Data;
            var byteOrderAware = Message.Datatype.BitField as IByteOrderAware;
            var destination = buffer;
            var source = destination.ToArray();

            if (byteOrderAware is not null)
                H5Utils.EnsureEndianness(source, destination, byteOrderAware.ByteOrder, Message.Datatype.Size);

            return MemoryMarshal
                .Cast<byte, T>(Message.Data)
                .ToArray();
        }

        public T[] ReadCompound<T>() 
            where T : struct
        {
            return ReadCompound<T>(fieldInfo => fieldInfo.Name);
        }

        public unsafe T[] ReadCompound<T>(Func<FieldInfo, string> getName) 
            where T : struct
        {
            return H5Utils.ReadCompound<T>(Message.Datatype, Message.Dataspace, _superblock, Message.Data, getName);
        }

        public string[] ReadString()
        {
            return H5Utils.ReadString(Message.Datatype, Message.Data, _superblock);
        }

        #endregion
    }
}
