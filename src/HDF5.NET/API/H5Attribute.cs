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

        public unsafe T[] ReadCompound<T>(Func<FieldInfo, string>? getName = default) 
            where T : struct
        {
            if (getName is null)
                getName = fieldInfo => fieldInfo.Name;

            return H5ReadUtils.ReadCompound<T>(Message.Datatype, Message.Data, _superblock, getName);
        }

        public unsafe Dictionary<string, object?>[] ReadCompound()
        {
            return H5ReadUtils.ReadCompound(Message.Datatype, Message.Data, _superblock);
        }

        public string[] ReadString()
        {
            return H5ReadUtils.ReadString(Message.Datatype, Message.Data, _superblock);
        }

        #endregion
    }
}
