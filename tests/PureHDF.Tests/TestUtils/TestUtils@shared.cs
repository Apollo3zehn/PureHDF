using System.Runtime.InteropServices;
using System.Text;
using HDF.PInvoke;

namespace PureHDF.Tests
{
    public partial class TestUtils
    {
        public static unsafe void AddNumerical(long fileId, ContainerType container)
        {
            var dims = new ulong[] { 2, 2, 3 };

            foreach (var entry in ReadingTestData.NumericalReadData)
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
            Add(container, fileId, "bitfield", "bitfield", H5T.STD_B16LE, ReadingTestData.BitfieldData.AsSpan());
        }

        public static unsafe void AddOpaque(long fileId, ContainerType container)
        {
            var length = (ulong)ReadingTestData.SmallData.Length * 2;
            var typeId = H5T.create(H5T.class_t.OPAQUE, new IntPtr(2));
            _ = H5T.set_tag(typeId, "Opaque Test Tag");

            Add(container, fileId, "opaque", "opaque", typeId, ReadingTestData.SmallData.AsSpan(), length);

            _ = H5T.close(typeId);
        }

        public static unsafe void AddArray_value(long fileId, ContainerType container)
        {
            long res;

            var typeId = H5T.array_create(H5T.NATIVE_INT32, 2, new ulong[] { 4, 5 });
            var dims = new ulong[] { 2, 3 };

            fixed (void* dataPtr = ReadingTestData.ArrayDataValue)
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

            var dataVarChar = ReadingTestData.ArrayDataVariableLengthString
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

            using var nullableStruct = Prepare_nullable_struct();

            var typeIdArray = H5T.array_create(nullableStruct.TypeId, 2, new ulong[] { 2, 3 });
            Add(container, fileId, "array", "nullable_struct", typeIdArray, nullableStruct.Ptr.ToPointer(), dims);
            _ = H5T.close(typeIdArray);
        }

        public static unsafe void AddObjectReference(long fileId, ContainerType container)
        {
            long res;

            AddNumerical(fileId, ContainerType.Dataset);

            var length = (ulong)ReadingTestData.NumericalReadData.Count;
            var data = new ulong[length];

            fixed (ulong* ptr = data)
            {
                var referenceGroupId = H5G.open(fileId, "numerical");

                for (ulong i = 0; i < length; i++)
                {
                    res = H5R.create(new IntPtr(ptr + i), referenceGroupId, $"D{i + 1}", H5R.type_t.OBJECT, -1);
                }

                Add(container, fileId, "reference", "object", H5T.STD_REF_OBJ, new IntPtr(ptr).ToPointer(), length);

                res = H5G.close(referenceGroupId);
            }
        }

        public static unsafe void AddRegionReference(long fileId, ContainerType container)
        {
            long res;

            var dims = new ulong[] { 3, 4, 5 };
            Add(ContainerType.Dataset, fileId, "reference", "referenced", H5T.NATIVE_INT32, ReadingTestData.SmallData.AsSpan(), dims);

            var length = 5;
            var data = new NativeRegionReference1[length];

            fixed (NativeRegionReference1* ptr = data)
            {
                var referenceGroupId = H5G.open(fileId, "reference");
                var referencedDatasetId = H5D.open(referenceGroupId, "referenced");
                var referencedSpaceId = H5D.get_space(referencedDatasetId);

                // none selection
                res = H5S.select_none(referencedSpaceId);
                res = H5R.create(new IntPtr(ptr + 0), referenceGroupId, "referenced", H5R.type_t.DATASET_REGION, referencedSpaceId);

                // point selection
                var coordinates = new ulong[]
                {
                    0, 0, 2, // 2
                    1, 1, 2, // 27
                    2, 3, 4, // 59
                    2, 2, 0, // 50
                };

                res = H5S.select_elements(referencedSpaceId, H5S.seloper_t.SET, new IntPtr(4), coordinates);
                res = H5R.create(new IntPtr(ptr + 1), referenceGroupId, "referenced", H5R.type_t.DATASET_REGION, referencedSpaceId);

                // regular hyperslab selection (this does not really work - an irregular hyperslab is being created)
                res = H5S.select_hyperslab(referencedSpaceId, H5S.seloper_t.SET, start: new ulong[] { 0, 0, 0 }, stride: new ulong[] { 1, 1, 3 }, count: new ulong[] { 1, 1, 2 }, block: new ulong[] { 1, 1, 2 }); // 0, 1, 3, 4
                res = H5R.create(new IntPtr(ptr + 2), referenceGroupId, "referenced", H5R.type_t.DATASET_REGION, referencedSpaceId);

                // irregular hyperslab selection
                res = H5S.select_hyperslab(referencedSpaceId, H5S.seloper_t.SET, start: new ulong[] { 0, 0, 0 }, stride: new ulong[] { 1, 1, 3 }, count: new ulong[] { 1, 1, 2 }, block: new ulong[] { 1, 1, 2 }); // 0, 1, 3, 4
                res = H5R.create(new IntPtr(ptr + 3), referenceGroupId, "referenced", H5R.type_t.DATASET_REGION, referencedSpaceId);

                // all selection
                res = H5S.select_all(referencedSpaceId);
                res = H5R.create(new IntPtr(ptr + 4), referenceGroupId, "referenced", H5R.type_t.DATASET_REGION, referencedSpaceId);

                //
                Add(container, fileId, "reference", "region", H5T.STD_REF_DSETREG, new IntPtr(ptr).ToPointer(), (ulong)length);

                res = H5D.close(referencedDatasetId);
                res = H5S.close(referencedSpaceId);
                res = H5G.close(referenceGroupId);
            }
        }

        public static unsafe void AddStruct(long fileId, ContainerType container, bool includeH5NameAttribute = false)
        {
            var dims = new ulong[] { 2, 2, 3 }; /* "extendible contiguous non-external dataset not allowed" */

            // non-nullable struct
            var typeId = GetHdfTypeIdFromType(typeof(TestStructL1));
            Add(container, fileId, "struct", "nonnullable", typeId, ReadingTestData.NonNullableStructData.AsSpan(), dims);
            _ = H5T.close(typeId);

            // nullable struct
            using var nullableStruct = Prepare_nullable_struct(includeH5NameAttribute);
            Add(container, fileId, "struct", "nullable", nullableStruct.TypeId, nullableStruct.Ptr.ToPointer(), dims);
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

            // variable length string (UTF8)
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

        public static unsafe void AddVariableLengthSequence_Simple(long fileId, ContainerType container)
        {
            // based on: https://svn.ssec.wisc.edu/repos/geoffc/C/HDF5/Examples_by_API/h5ex_t_vlen.c

            var dims = new ulong[] { 3 };
            var typeId = H5T.vlen_create(H5T.NATIVE_INT32);

            // data 1 (countdown)
            var data1 = new int[3];

            for (int i = 0; i < data1.Length; i++)
                data1[i] = data1.Length - i;

            // data 2 (Fibonacci sequence)
            var data2 = new int[12];

            data2[0] = 1;
            data2[1] = 1;

            for (int i = 2; i < data2.Length; i++)
                data2[i] = data2[i - 1] + data2[i - 2];

            // data 3 (empty)

            //

            fixed (int* data1Ptr = data1, data2Ptr = data2)
            {
                // vlen data
                var vlenData = new H5T.hvl_t[] 
                {
                    new H5T.hvl_t() { len = (nint)data1.Length, p = (nint)data1Ptr },
                    new H5T.hvl_t() { len = (nint)data2.Length, p = (nint)data2Ptr }
                };

                fixed (void* vlenDataPtr = vlenData)
                {
                    Add(container, fileId, "sequence", "variable_simple", typeId, vlenDataPtr, dims);
                }
            }

            _ = H5T.close(typeId);
        }

        public static unsafe void AddVariableLengthSequence_NullableStruct(long fileId, ContainerType container)
        {
            using var data = Prepare_nullable_struct();

            var dims = new ulong[] { 2 };
            var typeId = H5T.vlen_create(data.TypeId);

            // vlen data
            var vlenData = new H5T.hvl_t[] 
            {
                // struct data
                new H5T.hvl_t() { len = (nint)data.Ptrs.Count, p = (nint)data.Ptr },

                // empty
                new H5T.hvl_t() { len = IntPtr.Zero, p = IntPtr.Zero },
            };

            fixed (void* vlenDataPtr = vlenData)
            {
                Add(container, fileId, "sequence", "variable_nullable_struct", typeId, vlenDataPtr, dims);
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
                    Add(container, fileId, "mass_attributes", name, typeId, ReadingTestData.NonNullableStructData.AsSpan(), dims, cpl: acpl_id);
                }
                else
                {
                    var name = $"mass_{i:D4}";
                    Add(container, fileId, "mass_attributes", name, typeId, ReadingTestData.NonNullableStructData.AsSpan(), dims);
                }
            }

            _ = H5T.close(typeId);
        }

        public static unsafe void AddSmall(long fileId, ContainerType container)
        {
            Add(container, fileId, "small", "small", H5T.NATIVE_INT32, ReadingTestData.SmallData.AsSpan());
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

        private static unsafe DisposableStruct Prepare_nullable_struct(bool includeH5NameAttribute = false)
        {
            var typeId = GetHdfTypeIdFromType(typeof(TestStructStringAndArray), includeH5NameAttribute: includeH5NameAttribute);
            var data = ReadingTestData.NullableStructData;

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
                // TODO why not write directly into dataPtr? Is this blocked by Marshal.DestroyStructure?
                Marshal.StructureToPtr(x, sourcePtr, fDeleteOld: false);

                ptrs.Add(sourcePtr);
                var source = new Span<byte>(sourcePtr.ToPointer(), elementSize);
                var target = new Span<byte>(IntPtr.Add(dataPtr, elementSize * counter).ToPointer(), elementSize);

                source.CopyTo(target);
                counter++;
            });

            return new DisposableStruct(dataPtr, ptrs, typeId);
        }

        private record class DisposableStruct(IntPtr Ptr, List<nint> Ptrs, long TypeId) : IDisposable
        {
            private bool _disposedValue;

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    Marshal.FreeHGlobal(Ptr);

                    foreach (var ptr in Ptrs)
                    {
                        Marshal.DestroyStructure<TestStructStringAndArray>(ptr);
                    }

                    _ = H5T.close(TypeId);

                    _disposedValue = true;
                }
            }

            ~DisposableStruct()
            {
                Dispose(disposing: false);
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}