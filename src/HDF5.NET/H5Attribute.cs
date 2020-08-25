using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

        public T[] ReadCompound<T>() where T : struct
        {
            return this.ReadCompound<T>(fieldInfo => fieldInfo.Name);
        }

        public unsafe T[] ReadCompound<T>(Func<FieldInfo, string> getName) where T : struct
        {
            if (this.Message.Datatype.Class != DatatypeMessageClass.Compount)
                throw new Exception($"This method can only be used for data type class '{DatatypeMessageClass.Compount}'.");

            var type = typeof(T);
            var fieldInfoMap = new Dictionary<string, FieldProperties>();

            foreach (var fieldInfo in type.GetFields())
            {
                var name = getName(fieldInfo);

                fieldInfoMap[name] = new FieldProperties()
                {
                    Offset = Marshal.OffsetOf(type, fieldInfo.Name),
                    Type = fieldInfo.FieldType
                };
            }

            var properties = this.Message.Datatype.Properties
                .Cast<CompoundPropertyDescription>()
                .ToList();

            var sourceOffset = 0UL;
            var sourceRawBytes = Message.Data;
            var sourceElementSize = this.Message.Datatype.Size;

            var targetArraySize = this.Message.Dataspace.DimensionSizes.Aggregate((x, y) => x * y);
            var targetArray = new T[targetArraySize];
            var targetElementSize = (ulong)Marshal.SizeOf<T>();

            for (int i = 0; i < targetArray.Length; i++)
            {
                var targetRawBytes = new byte[targetElementSize];

                foreach (var property in properties)
                {
                    var fieldInfo = fieldInfoMap[property.Name];
                    var fieldSize = (int)property.MemberTypeMessage.Size;

                    // strings
                    if (fieldInfo.Type == typeof(string))
                    {
                        var sourceIndex = (int)(sourceOffset + property.MemberByteOffset);
                        var sourceIndexEnd = sourceIndex + fieldSize;
                        var targetIndex = fieldInfo.Offset.ToInt64();
                        var value = H5Utils.ReadString(property.MemberTypeMessage, sourceRawBytes[sourceIndex..sourceIndexEnd], _superblock);
#error This is quick and dirty. GCHandle is not released.
                        var bytes = Encoding.UTF8.GetBytes(value[0]).Concat(new byte[] { 0 }).ToArray();

                        var a = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        var ptr = a.AddrOfPinnedObject().ToInt64();
                        var ptrbytes = BitConverter.GetBytes(ptr);

                        for (int j = 0; j < Marshal.SizeOf<IntPtr>(); j++)
                        {
                            targetRawBytes[targetIndex + j] = ptrbytes[j];
                        }
                    }
#warning To be implemented.
                    // to be implemented (nested nullable structs)
                    else if (false)
                    {

                    }
                    // other value types
                    else
                    {
                        for (uint j = 0; j < fieldSize; j++)
                        {
                            var sourceIndex = sourceOffset + property.MemberByteOffset + j;
                            var targetIndex = fieldInfo.Offset.ToInt64() + j;

                            targetRawBytes[targetIndex] = sourceRawBytes[sourceIndex];
                        }
                    }
                }

                sourceOffset += sourceElementSize;

                fixed (byte* ptr = targetRawBytes.AsSpan())
                {
                    targetArray[i] = Marshal.PtrToStructure<T>(new IntPtr(ptr));
                }
            }

            return targetArray;
        }

        public string[] ReadString()
        {
            return H5Utils.ReadString(this.Message.Datatype, this.Message.Data, _superblock);
        }

        #endregion
    }
}
