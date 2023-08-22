using System.Text;

namespace PureHDF;

internal static partial class ReadUtils
{
    public static string[] ReadString(
        NativeReadContext context,
        DatatypeMessage datatype,
        Span<byte> data)
    {
        var size = (int)datatype.Size;
        var elementCount = data.Length / size;
        var destination = new string[elementCount];

        ReadString(context, datatype, data, destination);

        return destination;
    }

    public static Memory<string> ReadString(
        NativeReadContext context,
        DatatypeMessage datatype,
        Span<byte> source,
        Memory<string> destination)
    {
        /* Padding
         * https://support.hdfgroup.org/HDF5/doc/H5.format.html#DatatypeMessage
         * Search for "null terminate": null terminate and null padding are essentially
         * the same when simply reading them from file.
         */
        var destinationSpan = destination.Span;
        var isFixed = datatype.Class == DatatypeMessageClass.String;

        if (isFixed)
        {
            var size = (int)datatype.Size;

            if (datatype.BitField is not StringBitFieldDescription bitField)
                throw new Exception("String bit field description must not be null.");

            var position = 0;

            Func<string, string> trim = bitField.PaddingType switch
            {
#if NETSTANDARD2_0
                PaddingType.NullTerminate => value => value.Split(new char[] { '\0' }, 2)[0],
#else
                PaddingType.NullTerminate => value => value.Split('\0', 2)[0],
#endif
                PaddingType.NullPad => value => value.TrimEnd('\0'),
                PaddingType.SpacePad => value => value.TrimEnd(' '),
                _ => throw new Exception("Unsupported padding type.")
            };

            for (int i = 0; i < destination.Length; i++)
            {
                var value = ReadFixedLengthString(source[position..(position + size)]);

                value = trim(value);
                destinationSpan[i] = value;
                position += size;
            }
        }

        else if (datatype.Class == DatatypeMessageClass.VariableLength)
        {
            /* String is always split after first \0 when writing data to file. 
             * In other words, padding type only matters when reading data.
             */

            if (datatype.BitField is not VariableLengthBitFieldDescription bitField)
                throw new Exception("Variable-length bit field description must not be null.");

            if (bitField.Type != InternalVariableLengthType.String)
                throw new Exception($"Variable-length type must be '{InternalVariableLengthType.String}'.");

            // see IV.B. Disk Format: Level 2B - Data Object Data Storage
            using var localDriver = new H5StreamDriver(new MemoryStream(source.ToArray()), leaveOpen: false);

            Func<string, string> trim = bitField.PaddingType switch
            {
                PaddingType.NullTerminate => value => value,
                PaddingType.NullPad => value => value,
                PaddingType.SpacePad => value => value.TrimEnd(' '),
                _ => throw new Exception("Unsupported padding type.")
            };

            for (int i = 0; i < destination.Length; i++)
            {
                /* Skip the length of the sequence (H5Tvlen.c H5T_vlen_disk_read) */
                var _ = localDriver.ReadUInt32();

                var globalHeapId = ReadingGlobalHeapId.Decode(context.Superblock, localDriver);

                if (globalHeapId.Equals(default))
                {
                    destinationSpan[i] = default!;
                    continue;
                }

                var globalHeapCollection = NativeCache.GetGlobalHeapObject(context, globalHeapId.CollectionAddress);

                if (globalHeapCollection.GlobalHeapObjects.TryGetValue((int)globalHeapId.ObjectIndex, out var globalHeapObject))
                {
                    var value = Encoding.UTF8.GetString(globalHeapObject.ObjectData);
                    value = trim(value);
                    destinationSpan[i] = value;
                }

                else
                {
                    // It would be more correct to just throw an exception 
                    // when the object index is not found in the collection,
                    // but that would make the tests following tests fail
                    // - CanReadDataset_Array_nullable_struct
                    // - CanReadDataset_Array_nullable_struct.
                    // 
                    // And it would make the user's life a bit more complicated
                    // if the library cannot handle missing entries.
                    // 
                    // Since this behavior is not according to the spec, this
                    // method still returns a `string` instead of a nullable 
                    // `string?`.
                    destinationSpan[i] = default!;
                }
            }
        }

        else
        {
            throw new Exception($"Data type class '{datatype.Class}' cannot be read as string.");
        }

        return destination;
    }

    public static string ReadFixedLengthString(Span<byte> data, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
    {
#if NETSTANDARD2_0
            return encoding switch
            {
                CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data.ToArray()),
                CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data.ToArray()),
                _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
            };
#else
        return encoding switch
        {
            CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data),
            CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data),
            _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
        };
#endif
    }

    public static string ReadFixedLengthString(H5DriverBase driver, int length, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
    {
        var data = driver.ReadBytes(length);

        return encoding switch
        {
            CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data),
            CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data),
            _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
        };
    }

    public static string ReadNullTerminatedString(H5DriverBase driver, bool pad, int padSize = 8, CharacterSetEncoding encoding = CharacterSetEncoding.ASCII)
    {
        var data = new List<byte>();
        var byteValue = driver.ReadByte();

        while (byteValue != '\0')
        {
            data.Add(byteValue);
            byteValue = driver.ReadByte();
        }

        var destination = encoding switch
        {
            CharacterSetEncoding.ASCII => Encoding.ASCII.GetString(data.ToArray()),
            CharacterSetEncoding.UTF8 => Encoding.UTF8.GetString(data.ToArray()),
            _ => throw new FormatException($"The character set encoding '{encoding}' is not supported.")
        };

        if (pad)
        {
            // https://stackoverflow.com/questions/20844983/what-is-the-best-way-to-calculate-number-of-padding-bytes
            var paddingCount = (padSize - (destination.Length + 1) % padSize) % padSize;
            driver.Seek(paddingCount, SeekOrigin.Current);
        }

        return destination;
    }
}