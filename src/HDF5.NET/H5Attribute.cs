using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public class H5Attribute
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

        public Span<T> Read<T>() where T : unmanaged
        {
            return MemoryMarshal
                .Cast<byte, T>(this.Message.Data);
        }

        public List<string> ReadAsStringArray()
        {
            var isFixed = this.Message.Datatype.Class == DatatypeMessageClass.String;

            if (!isFixed && this.Message.Datatype.Class != DatatypeMessageClass.VariableLength)
                throw new Exception($"Attribute data type class '{this.Message.Datatype.Class}' cannot be read as string.");

            var data = this.Message.Data;
            var size = (int)this.Message.Datatype.Size;
            var result = new List<string>();

            if (isFixed)
            {
                var description = this.Message.Datatype.BitFieldDescription as StringBitFieldDescription;

                if (description == null)
                    throw new Exception("String bit field desciption must not be null.");

                if (description.PaddingType != PaddingType.NullTerminate)
                    throw new Exception($"Only padding type '{PaddingType.NullTerminate}' is supported.");

                using (var reader = new BinaryReader(new MemoryStream(data)))
                {
                    while (reader.BaseStream.Position != data.Length)
                    {
                        var value = H5Utils.ReadFixedLengthString(reader, size);
                        result.Add(value);
                    }
                }
            }
            else
            {
                var description = this.Message.Datatype.BitFieldDescription as VariableLengthBitFieldDescription;

                if (description == null)
                    throw new Exception("Variable-length bit field desciption must not be null.");

                if (description.Type != VariableLengthType.String)
                    throw new Exception($"Variable-length type must be '{VariableLengthType.String}'.");

                if (description.PaddingType != PaddingType.NullTerminate)
                    throw new Exception($"Only padding type '{PaddingType.NullTerminate}' is supported.");

                // see IV.B. Disk Format: Level 2B - Data Object Data Storage
                using (var dataReader = new BinaryReader(new MemoryStream(data)))
                {
                    while (dataReader.BaseStream.Position != data.Length)
                    {
                        var dataSize = dataReader.ReadUInt32();
                        var globalHeapId = new GlobalHeapId(dataReader, _superblock);
                        var globalHeapCollection = globalHeapId.Collection;
                        var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex - 1];
                        var value = Encoding.UTF8.GetString(globalHeapObject.ObjectData);

                        result.Add(value);
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
