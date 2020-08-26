using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

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

                var isNotSupported = H5Utils.IsReferenceOrContainsReferences(fieldInfo.FieldType)
                                  && fieldInfo.FieldType != typeof(string);

                if (isNotSupported)
                    throw new Exception("Nested nullable fields are not supported.");

                fieldInfoMap[name] = new FieldProperties()
                {
                    FieldInfo = fieldInfo,
                    Offset = Marshal.OffsetOf(type, fieldInfo.Name)
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
            var targetElementSize = Marshal.SizeOf<T>();

            for (int i = 0; i < targetArray.Length; i++)
            {
                var targetRawBytes = new byte[targetElementSize];
                var stringMap = new Dictionary<FieldProperties, string>();

                foreach (var property in properties)
                {
                    var fieldInfo = fieldInfoMap[property.Name];
                    var fieldSize = (int)property.MemberTypeMessage.Size;

                    // strings
                    if (fieldInfo.FieldInfo.FieldType == typeof(string))
                    {
                        var sourceIndex = (int)(sourceOffset + property.MemberByteOffset);
                        var sourceIndexEnd = sourceIndex + fieldSize;
                        var targetIndex = fieldInfo.Offset.ToInt64();
                        var value = H5Utils.ReadString(property.MemberTypeMessage, sourceRawBytes[sourceIndex..sourceIndexEnd], _superblock);
                        
                        stringMap[fieldInfo] = value[0];
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
                    // http://benbowen.blog/post/fun_with_makeref/
                    // https://stackoverflow.com/questions/4764573/why-is-typedreference-behind-the-scenes-its-so-fast-and-safe-almost-magical
                    // Both do not work because struct layout is different with __makeref:
                    // https://stackoverflow.com/questions/1918037/layout-of-net-value-type-in-memory
                    targetArray[i] = Marshal.PtrToStructure<T>(new IntPtr(ptr));

                    foreach (var entry in stringMap)
                    {
                        var reference = __makeref(targetArray[i]);
                        entry.Key.FieldInfo.SetValueDirect(reference, entry.Value);
                    }
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
