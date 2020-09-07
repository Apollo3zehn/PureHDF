using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace HDF5.NET
{
    internal static class H5Utils
    {
        public static void ValidateSignature(byte[] actual, byte[] expected)
        {
            var actualString = Encoding.ASCII.GetString(actual);
            var expectedString = Encoding.ASCII.GetString(expected);

            if (actualString != expectedString)
                throw new Exception($"The actual signature '{actualString}' does not match the expected signature '{expectedString}'.");
        }

        public static bool IsPowerOfTwo(ulong x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        public static ulong FindMinByteCount(ulong value)
        {
            ulong lg_v = 1;

            while ((value >>= 1) != 0)
            {
                lg_v++;
            }

            ulong result = lg_v >> 3;

            if (lg_v != result << 3)
                result += 1;

            return result;
        }

        public static string[] ReadString(DatatypeMessage datatype, Span<byte> data, Superblock superblock)
        {
            var isFixed = datatype.Class == DatatypeMessageClass.String;

            if (!isFixed && datatype.Class != DatatypeMessageClass.VariableLength)
                throw new Exception($"Attribute data type class '{datatype.Class}' cannot be read as string.");

            var size = (int)datatype.Size;
            var result = new List<string>();

            if (isFixed)
            {
                var bitField = datatype.BitField as StringBitFieldDescription;

                if (bitField == null)
                    throw new Exception("String bit field desciption must not be null.");

                if (bitField.PaddingType != PaddingType.NullTerminate)
                    throw new Exception($"Only padding type '{PaddingType.NullTerminate}' is supported.");

                var position = 0;

                while (position != data.Length)
                {
                    var value = H5Utils.ReadFixedLengthString(data[position..(position + size)]);
                    result.Add(value);
                    position += size;
                }
            }
            else
            {
                var bitField = datatype.BitField as VariableLengthBitFieldDescription;

                if (bitField == null)
                    throw new Exception("Variable-length bit field desciption must not be null.");

                if (bitField.Type != VariableLengthType.String)
                    throw new Exception($"Variable-length type must be '{VariableLengthType.String}'.");

                if (bitField.PaddingType != PaddingType.NullTerminate)
                    throw new Exception($"Only padding type '{PaddingType.NullTerminate}' is supported.");

                // see IV.B. Disk Format: Level 2B - Data Object Data Storage
                using (var dataReader = new BinaryReader(new MemoryStream(data.ToArray())))
                {
                    while (dataReader.BaseStream.Position != data.Length)
                    {
                        var dataSize = dataReader.ReadUInt32(); // for what do we need this?
                        var globalHeapId = new GlobalHeapId(dataReader, superblock);
                        var globalHeapCollection = globalHeapId.Collection;
                        var globalHeapObject = globalHeapCollection.GlobalHeapObjects[(int)globalHeapId.ObjectIndex - 1];
                        var value = Encoding.UTF8.GetString(globalHeapObject.ObjectData);

                        result.Add(value);
                    }
                }
            }

            return result.ToArray();
        }

        public static string ReadFixedLengthString(Span<byte> data, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
            return encoding switch
            {
                CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data),
                CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
        }

        public static string ReadFixedLengthString(BinaryReader reader, int length, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
            var data = reader.ReadBytes(length);

            return encoding switch
            {
                CharacterSetEncoding.ASCII  => Encoding.ASCII.GetString(data),
                CharacterSetEncoding.UTF8   => Encoding.UTF8.GetString(data),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
        }

        public static string ReadNullTerminatedString(BinaryReader reader, bool pad, int padSize = 8, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
        {
            var data = new List<byte>();
            var byteValue = reader.ReadByte();

            while (byteValue != '\0')
            {
                data.Add(byteValue);
                byteValue = reader.ReadByte();
            }

            var result = encoding switch
            {
                CharacterSetEncoding.ASCII  => Encoding.ASCII.GetString(data.ToArray()),
                CharacterSetEncoding.UTF8   => Encoding.UTF8.GetString(data.ToArray()),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };

            if (pad)
            {
                // https://stackoverflow.com/questions/20844983/what-is-the-best-way-to-calculate-number-of-padding-bytes
                var paddingCount = (padSize - (result.Length + 1) % padSize) % padSize;
                reader.BaseStream.Seek(paddingCount, SeekOrigin.Current);
            }

            return result;
        }

        public static ulong ReadUlong(BinaryReader reader, ulong size)
        {
            return size switch
            {
                1 => reader.ReadByte(),
                2 => reader.ReadUInt16(),
                4 => reader.ReadUInt32(),
                8 => reader.ReadUInt64(),
                _ => H5Utils.ReadUlongArbitrary(reader, size)
            };
        }

        public static bool IsReferenceOrContainsReferences(Type type)
        {
            var name = nameof(RuntimeHelpers.IsReferenceOrContainsReferences);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance;
            var method = typeof(RuntimeHelpers).GetMethod(name, flags);
            var generic = method.MakeGenericMethod(type);

            return (bool)generic.Invoke(null, null);
        }

        private static ulong ReadUlongArbitrary(BinaryReader reader, ulong size)
        {
            var result = 0UL;
            var shift = 0;

            for (ulong i = 0; i < size; i++)
            {
                var value = reader.ReadByte();
                result += (ulong)(value << shift);
                shift += 8;
            }

            return result;
        }
    }
}
