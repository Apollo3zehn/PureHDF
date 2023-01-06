using System.Reflection;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    /// <summary>
    /// An HDF5 attribute.
    /// </summary>
    public partial class H5Attribute
    {
        #region Properties

        /// <summary>
        /// Gets the attribute name.
        /// </summary>
        public string Name => Message.Name;

        /// <summary>
        /// Gets the data space.
        /// </summary>
        public H5Dataspace Space
        {
            get
            {
                if (_space is null)
                    _space = new H5Dataspace(Message.Dataspace);

                return _space;
            }
        }

        /// <summary>
        /// Gets the data type.
        /// </summary>
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

        /// <summary>
        /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
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

        /// <summary>
        /// Reads the compound data. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
        /// </summary>
        /// <typeparam name="T">The type of the data to read.</typeparam>
        /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
        /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
        public T[] ReadCompound<T>(Func<FieldInfo, string>? getName = default) 
            where T : struct
        {
            if (getName is null)
                getName = fieldInfo => fieldInfo.Name;

            return H5ReadUtils.ReadCompound<T>(Message.Datatype, Message.Data, _superblock, getName);
        }

        /// <summary>
        /// Reads the compound data. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
        /// </summary>
        /// <returns>The read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
        public Dictionary<string, object?>[] ReadCompound()
        {
            return H5ReadUtils.ReadCompound(Message.Datatype, Message.Data, _superblock);
        }

        /// <summary>
        /// Reads the string data.
        /// </summary>
        /// <returns>The read data as array of <see cref="string"/>.</returns>
        public string[] ReadString()
        {
            return H5ReadUtils.ReadString(Message.Datatype, Message.Data, _superblock);
        }

        #endregion
    }
}
