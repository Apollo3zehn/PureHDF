using HDF.PInvoke;
using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddNumerical(long fileId, ContainerType container)
        {
            var dims = new ulong[] { 2, 2, 3 };

            foreach (var entry in TestData.NumericalData)
            {
                var attributeData = (Array)entry[1];

                var type = attributeData
                    .GetType()
                    .GetElementType()!;

                var typeId = GetHdfTypeIdFromType(type);

                if (type == typeof(TestEnum))
                {
                    attributeData = attributeData
                        .OfType<object>()
                        .Select(value => (short)value)
                        .ToArray();
                }

                var handle = GCHandle.Alloc(attributeData, GCHandleType.Pinned);
                var ptr = handle.AddrOfPinnedObject().ToPointer();

                Add(container, fileId, "numerical", (string)entry[0], typeId, ptr, dims);

                handle.Free();
            }
        }

        public static unsafe void AddBitField(long fileId, ContainerType container)
        {
            Add(container, fileId, "bitfield", "bitfield", H5T.STD_B16LE, TestData.BitfieldData.AsSpan());
        }

        public static unsafe void AddOpaque(long fileId, ContainerType container)
        {
            var length = (ulong)TestData.SmallData.Length * 2;
            var typeId = H5T.create(H5T.class_t.OPAQUE, new IntPtr(2));
            _ = H5T.set_tag(typeId, "Opaque Test Tag");

            Add(container, fileId, "opaque", "opaque", typeId, TestData.SmallData.AsSpan(), length);

            _ = H5T.close(typeId);
        }

        public static unsafe void AddArray_value(long fileId, ContainerType container)
        {
            long res;

            var typeId = H5T.array_create(H5T.NATIVE_INT32, 2, new ulong[] { 4, 5 });
            var dims = new ulong[] { 2, 3 };

            fixed (void* dataPtr = TestData.ArrayDataValue)
            {
                Add(container, fileId, "array", "value", typeId, dataPtr, dims);
            }

            res = H5T.close(typeId);
        }

        public static unsafe void AddArray_variable_length_string(long fileId, ContainerType container)
        {
            long res;

            var typeIdVar = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdVar, H5T.VARIABLE);

            var typeId = H5T.array_create(typeIdVar, 2, new ulong[] { 4, 5 });
            var dims = new ulong[] { 2, 3 };

            var offset = 0;
            var offsets = new List<int>();

            var dataVarChar = TestData.ArrayDataVariableLengthString
                .Cast<string>()
                .SelectMany(value => 
                {
                    var bytes = Encoding.ASCII.GetBytes(value + '\0');
                    offsets.Add(offset);
                    offset += bytes.Length;
                    return bytes;
                })
                .ToArray();

            fixed (byte* dataVarPtr = dataVarChar)
            {
                var basePtr = new IntPtr(dataVarPtr);

                var addresses = offsets
                    .Select(offset => IntPtr.Add(basePtr, offset))
                    .ToArray();

                fixed (void* dataVarAddressesPtr = addresses)
                {
                    Add(container, fileId, "array", "variable_length_string", typeId, dataVarAddressesPtr, dims);
                }
            }

            res = H5T.close(typeIdVar);
            res = H5T.close(typeId);
        }

        public static unsafe void AddArray_nullable_struct(long fileId, ContainerType container)
        {
            var dims = new ulong[] { 2, 1 };

            Prepare_nullable_struct((typeId, dataPtr) => 
            {
                var typeIdArray = H5T.array_create(typeId, 2, new ulong[] { 2, 3 });
                Add(container, fileId, "array", "nullable_struct", typeIdArray, dataPtr.ToPointer(), dims);
                _ = H5T.close(typeIdArray);
            });
        }

        public static unsafe void AddObjectReference(long fileId, ContainerType container)
        {
            long res;

            AddNumerical(fileId, ContainerType.Dataset);

            var length = (ulong)TestData.NumericalData.Count;
            var data = new ulong[length];

            fixed (ulong* ptr = data)
            {
                var referenceGroupId = H5G.open(fileId, "numerical");

                for (ulong i = 0; i < length; i++)
                {
                    res = H5R.create(new IntPtr(ptr + i), referenceGroupId, $"D{i + 1}", H5R.type_t.OBJECT, -1);
                }

                Add(container, fileId, "reference", "object_reference", H5T.STD_REF_OBJ, new IntPtr(ptr).ToPointer(), length);

                res = H5G.close(referenceGroupId);
            }
        }

        public static unsafe void AddRegionReference(long fileId, ContainerType container)
        {
            long res;

            AddSmall(fileId, ContainerType.Dataset);

            var length = 1UL;
            var data = new ulong[length];

            fixed (ulong* ptr = data)
            {
                var referenceGroupId = H5G.open(fileId, "small");
                var spaceId = H5S.create_simple(1, new ulong[] { length }, null);
                var coordinates = new ulong[] { 2, 4, 6, 8 };
                res = H5S.select_elements(spaceId, H5S.seloper_t.SET, new IntPtr(4), coordinates);
                res = H5R.create(new IntPtr(ptr), referenceGroupId, "small", H5R.type_t.DATASET_REGION, spaceId);

                Add(container, fileId, "reference", "region_reference", H5T.STD_REF_DSETREG, new IntPtr(ptr).ToPointer(), length);

                res = H5S.close(spaceId);
                res = H5G.close(referenceGroupId);
            }
        }

        public static unsafe void AddStruct(long fileId, ContainerType container)
        {
            long res;

            var dims = new ulong[] { 2, 2, 3 }; /* "extendible contiguous non-external dataset not allowed" */

            // non-nullable struct
            var typeId = GetHdfTypeIdFromType(typeof(TestStructL1));
            Add(container, fileId, "struct", "nonnullable", typeId, TestData.NonNullableStructData.AsSpan(), dims);
            res = H5T.close(typeId);

            // nullable struct
            Prepare_nullable_struct((typeId, dataPtr) 
                => Add(container, fileId, "struct", "nullable", typeId, dataPtr.ToPointer(), dims));
        }

        public static unsafe void AddString(long fileId, ContainerType container)
        {
            long res;

            var dims = new ulong[] { 2, 2, 3 }; /* "extendible contiguous non-external dataset not allowed" */

            // fixed length string + null terminate (ASCII)
            var typeIdFixed_nullterm = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdFixed_nullterm, new IntPtr(4));
            res = H5T.set_cset(typeIdFixed_nullterm, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdFixed_nullterm, H5T.str_t.NULLTERM);

            var dataFixed_nullterm = new string[] { "00\00", "11\0 ", "22\0 ", "3\0  ", "44 \0", "555\0", "66 \0", "77\0 ", "  \0 ", "AA \0", "ZZ \0", "!!\0 " };
            var dataFixedChar_nullterm = dataFixed_nullterm
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            Add(container, fileId, "string", "fixed+nullterm", typeIdFixed_nullterm, dataFixedChar_nullterm.AsSpan(), dims);

            res = H5T.close(typeIdFixed_nullterm);

            // fixed length string + null padding (ASCII)
            var typeIdFixed_nullpad = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdFixed_nullpad, new IntPtr(4));
            res = H5T.set_cset(typeIdFixed_nullpad, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdFixed_nullpad, H5T.str_t.NULLPAD);

            var dataFixed_nullpad = new string[] { "0\00\0", "11\0\0", "22\0\0", "3 \0\0", " 4\0\0", "55 5", "66\0\0", "77\0\0", "  \0\0", "AA\0\0", "ZZ\0\0", "!!\0\0" };
            var dataFixedChar_nullpad = dataFixed_nullpad
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            Add(container, fileId, "string", "fixed+nullpad", typeIdFixed_nullpad, dataFixedChar_nullpad.AsSpan(), dims);

            res = H5T.close(typeIdFixed_nullpad);

            // fixed length string + space padding (ASCII)
            var typeIdFixed_spacepad = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdFixed_spacepad, new IntPtr(4));
            res = H5T.set_cset(typeIdFixed_spacepad, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdFixed_spacepad, H5T.str_t.SPACEPAD);

            var dataFixed_spacepad = new string[] { "00  ", "11  ", "22  ", "3   ", " 4  ", "55 5", "66  ", "77  ", "    ", "AA  ", "ZZ  ", "!!  " };
            var dataFixedChar_spacepad = dataFixed_spacepad
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            Add(container, fileId, "string", "fixed+spacepad", typeIdFixed_spacepad, dataFixedChar_spacepad.AsSpan(), dims);

            res = H5T.close(typeIdFixed_spacepad);

            // variable length string (ASCII)
            var typeIdVar = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdVar, H5T.VARIABLE);
            res = H5T.set_cset(typeIdVar, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdVar, H5T.str_t.NULLPAD);

            var dataVar = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var dataVarChar = dataVar
               .SelectMany(value => Encoding.ASCII.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtr = dataVarChar)
            {
                var basePtr = new IntPtr(dataVarPtr);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 4), IntPtr.Add(basePtr, 7), IntPtr.Add(basePtr, 10),
                    IntPtr.Add(basePtr, 13), IntPtr.Add(basePtr, 16), IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22),
                    IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28), IntPtr.Add(basePtr, 31), IntPtr.Add(basePtr, 34)
                };

                fixed (void* dataVarAddressesPtr = addresses)
                {
                    Add(container, fileId, "string", "variable", typeIdVar, dataVarAddressesPtr, dims);
                }
            }

            res = H5T.close(typeIdVar);

            // variable length string + space padding (ASCII)
            var typeIdVar_spacepad = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdVar_spacepad, H5T.VARIABLE);
            res = H5T.set_cset(typeIdVar_spacepad, H5T.cset_t.ASCII);
            res = H5T.set_strpad(typeIdVar_spacepad, H5T.str_t.SPACEPAD);

            var dataVar_spacepad = new string[] { "001  ", "1 1 ", "22  ", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var dataVarChar_spacepad = dataVar_spacepad
               .SelectMany(value => Encoding.ASCII.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtr_spacepad = dataVarChar_spacepad)
            {
                var basePtr = new IntPtr(dataVarPtr_spacepad);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 6), IntPtr.Add(basePtr, 11), IntPtr.Add(basePtr, 16),
                    IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22), IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28),
                    IntPtr.Add(basePtr, 31), IntPtr.Add(basePtr, 34), IntPtr.Add(basePtr, 37), IntPtr.Add(basePtr, 40)
                };

                fixed (void* addressesPtr = addresses)
                {
                    Add(container, fileId, "string", "variable+spacepad", typeIdVar_spacepad, addressesPtr, dims);
                }
            }

            res = H5T.close(typeIdVar_spacepad);

            // variable length string attribute (UTF8)
            var typeIdVarUTF8 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(typeIdVarUTF8, H5T.VARIABLE);
            res = H5T.set_cset(typeIdVarUTF8, H5T.cset_t.UTF8);
            res = H5T.set_strpad(typeIdVarUTF8, H5T.str_t.NULLPAD);

            var dataVarUTF8 = new string[] { "00", "111", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" };
            var dataVarCharUTF8 = dataVarUTF8
               .SelectMany(value => Encoding.UTF8.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtrUTF8 = dataVarCharUTF8)
            {
                var basePtr = new IntPtr(dataVarPtrUTF8);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 3), IntPtr.Add(basePtr, 7), IntPtr.Add(basePtr, 10),
                    IntPtr.Add(basePtr, 13), IntPtr.Add(basePtr, 16), IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22),
                    IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28), IntPtr.Add(basePtr, 33), IntPtr.Add(basePtr, 40)
                };

                fixed (void* addressesPtr = addresses)
                {
                    Add(container, fileId, "string", "variableUTF8", typeIdVarUTF8, addressesPtr, dims);
                }
            }
        }

        public static unsafe void AddVariableLengthSequence(long fileId, ContainerType container)
        {
            // https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Tpublic.h#L1621-L1642
            // https://portal.hdfgroup.org/display/HDF5/Datatype+Basics#DatatypeBasics-variable
            // https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/test/tarray.c#L1113
            // https://github.com/HDFGroup/hdf5/blob/hdf5_1_10_9/src/H5Tpublic.h#L234-L241

            // typedef struct {
            //     size_t len; /**< Length of VL data (in base type units) */
            //     void  *p;   /**< Pointer to VL data */
            // } hvl_t;

            var dims = new ulong[] { 10 };
            var typeId = H5T.vlen_create(H5T.NATIVE_INT32);

            var dataVar = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var dataVarChar = dataVar
               .SelectMany(value => Encoding.ASCII.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtr = dataVarChar)
            {
                var basePtr = new IntPtr(dataVarPtr);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 4), IntPtr.Add(basePtr, 7), IntPtr.Add(basePtr, 10),
                    IntPtr.Add(basePtr, 13), IntPtr.Add(basePtr, 16), IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22),
                    IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28), IntPtr.Add(basePtr, 31), IntPtr.Add(basePtr, 34)
                };

                fixed (void* dataVarAddressesPtr = addresses)
                {
                    Add(container, fileId, "sequence", "variable", typeId, dataVarAddressesPtr, dims);
                }
            }

            _ = H5T.close(typeId);
        }

        public static unsafe void AddMass(long fileId, ContainerType container)
        {
            var typeId = GetHdfTypeIdFromType(typeof(TestStructL1));

            for (int i = 0; i < 1000; i++)
            {
                var dims = new ulong[] { 2, 2, 3 };

                if (i == 450)
                {
                    var acpl_id = H5P.create(H5P.ATTRIBUTE_CREATE);
                    _ = H5P.set_char_encoding(acpl_id, H5T.cset_t.UTF8);
                    var name = "字形碼 / 字形码, Zìxíngmǎ";
                    Add(container, fileId, "mass_attributes", name, typeId, TestData.NonNullableStructData.AsSpan(), dims, cpl: acpl_id);
                }
                else
                {
                    var name = $"mass_{i:D4}";
                    Add(container, fileId, "mass_attributes", name, typeId, TestData.NonNullableStructData.AsSpan(), dims);
                }
            }

            _ = H5T.close(typeId);
        }

        public static unsafe void AddSmall(long fileId, ContainerType container)
        {
            Add(container, fileId, "small", "small", H5T.NATIVE_INT32, TestData.SmallData.AsSpan());
        }

        public static unsafe void AddDataWithSharedDataType(long fileId, ContainerType container)
        {
            long typeId = H5T.copy(H5T.C_S1);

            _ = H5T.set_size(typeId, H5T.VARIABLE);
            _ = H5T.set_cset(typeId, H5T.cset_t.UTF8);
            _ = H5T.commit(fileId, "string_t", typeId);

            var data = new string[] { "001", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var dataChar = data
               .SelectMany(value => Encoding.ASCII.GetBytes(value + '\0'))
               .ToArray();

            fixed (byte* dataVarPtr = dataChar)
            {
                var basePtr = new IntPtr(dataVarPtr);

                var addresses = new IntPtr[]
                {
                    IntPtr.Add(basePtr, 0), IntPtr.Add(basePtr, 4), IntPtr.Add(basePtr, 7), IntPtr.Add(basePtr, 10),
                    IntPtr.Add(basePtr, 13), IntPtr.Add(basePtr, 16), IntPtr.Add(basePtr, 19), IntPtr.Add(basePtr, 22),
                    IntPtr.Add(basePtr, 25), IntPtr.Add(basePtr, 28), IntPtr.Add(basePtr, 31), IntPtr.Add(basePtr, 34)
                };

                fixed (void* dataVarAddressesPtr = addresses)
                {
                    Add(container, fileId, "shared_data_type", "shared_data_type", typeId, dataVarAddressesPtr, length: 12);
                }
            }

            if (H5I.is_valid(typeId) > 0) { _ = H5T.close(typeId); }
        }

        private static unsafe void Prepare_nullable_struct(Action<long, IntPtr> action)
        {
            var typeId = GetHdfTypeIdFromType(typeof(TestStructStringAndArray));
            var data = TestData.NullableStructData;

            // There is also Unsafe.SizeOf<T>() to calculate managed size instead of native size.
            // Is only relevant when Marshal.XX methods are replaced by other code.
            var elementSize = Marshal.SizeOf<TestStructStringAndArray>();
            var totalByteLength = elementSize * data.Length;
            var dataPtr = Marshal.AllocHGlobal(totalByteLength);
            var ptrs = new List<nint>();
            var counter = 0;

            data.Cast<ValueType>().ToList().ForEach(x =>
            {
                var sourcePtr = Marshal.AllocHGlobal(elementSize);
                Marshal.StructureToPtr(x, sourcePtr, false);

                ptrs.Add(sourcePtr);
                var source = new Span<byte>(sourcePtr.ToPointer(), elementSize);
                var target = new Span<byte>(IntPtr.Add(dataPtr, elementSize * counter).ToPointer(), elementSize);

                source.CopyTo(target);
                counter++;
            });

            action(typeId, dataPtr);

            Marshal.FreeHGlobal(dataPtr);

            foreach (var srcPtr in ptrs)
            {
                Marshal.DestroyStructure<TestStructStringAndArray>(srcPtr);
            }

            _ = H5T.close(typeId);
        }
    }
}