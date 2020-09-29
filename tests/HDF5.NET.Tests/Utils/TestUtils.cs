using HDF.PInvoke;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace HDF5.NET.Tests
{
    public class TestUtils
    {
        public static void RunForAllVersions(Action<H5F.libver_t> action)
        {
            var versions = new H5F.libver_t[]
            {
                H5F.libver_t.EARLIEST,
                H5F.libver_t.V18,
                H5F.libver_t.V110
            };

            foreach (var version in versions)
            {
                action(version);
            }
        }

        public static void RunForVersions(H5F.libver_t[] versions, Action<H5F.libver_t> action)
        {
            foreach (var version in versions)
            {
                action(version);
            }
        }

        public static unsafe string PrepareTestFile(H5F.libver_t version, Action<long> action)
        {
            var filePath = Path.GetTempFileName();
            long res;

            // file
            var faplId = H5P.create(H5P.FILE_ACCESS);
            res = H5P.set_libver_bounds(faplId, version, version);
            var fileId = H5F.create(filePath, H5F.ACC_TRUNC, 0, faplId);
            action?.Invoke(fileId);
            res = H5F.close(fileId);

            return filePath;
        }

        public static unsafe void AddSimple(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "simple");
            var groupId_sub = H5G.create(groupId, "sub");

            // datasets
            var dataspaceId1 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId1 = H5D.create(fileId, "D", H5T.NATIVE_INT8, dataspaceId1);
            var data1 = new byte[] { 1 };

            fixed (void* ptr = data1)
            {
                res = H5D.write(datasetId1, H5T.NATIVE_INT8, dataspaceId1, dataspaceId1, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId1);
            res = H5S.close(dataspaceId1);

            var dataspaceId2 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId2 = H5D.create(groupId, "D1", H5T.NATIVE_INT8, dataspaceId2);

            res = H5D.close(datasetId2);
            res = H5S.close(dataspaceId2);

            var dataspaceId3 = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId3 = H5D.create(groupId_sub, "D1.1", H5T.NATIVE_INT8, dataspaceId3);

            res = H5D.close(datasetId3);
            res = H5S.close(dataspaceId3);

            res = H5G.close(groupId);
            res = H5G.close(groupId_sub);
        }

        public static unsafe void AddMassLinks(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "mass_links");

            for (int i = 0; i < 1000; i++)
            {
                var linkId = H5G.create(groupId, $"mass_{i.ToString("D4")}");
                res = H5G.close(linkId);
            }

            res = H5G.close(groupId);
        }

        public static unsafe void AddNumericalAttributes(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "numerical");
            var spaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 3, 3, 4 });

            // numeric attributes
            foreach (var entry in TestData.AttributeNumericalData)
            {
                var attributeData = (Array)entry[1];
                var type = attributeData
                    .GetType()
                    .GetElementType();

                var typeId = TestUtils.GetHdfTypeIdFromType(type);
                var attributeId = H5A.create(groupId, (string)entry[0], typeId, spaceId);

                if (type == typeof(TestEnum))
                {
                    attributeData = attributeData
                        .OfType<object>()
                        .Select(value => (short)value)
                        .ToArray();
                }

                var handle = GCHandle.Alloc(attributeData, GCHandleType.Pinned);
                var ptr = handle.AddrOfPinnedObject().ToPointer();
                res = H5A.write(attributeId, typeId, new IntPtr(ptr));

                handle.Free();

                res = H5A.close(attributeId);
            }

            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddBitFieldAttribute(long fileId)
        {
            long res;

            var length = (ulong)TestData.BitfieldData.Length;
            var groupId = H5G.create(fileId, "bitfield");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });

            var attributeData = TestData.BitfieldData;
            var attributeId = H5A.create(groupId, "bitfield", H5T.STD_B16LE, spaceId);

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.STD_B16LE, new IntPtr(ptr));
            }

            res = H5A.close(attributeId);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddOpaqueAttribute(long fileId)
        {
            long res;

            var length = (ulong)TestData.SmallData.Length * 2;
            var groupId = H5G.create(fileId, "opaque");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });

            var attributeData = TestData.SmallData;
            var attributeTypeId = H5T.create(H5T.class_t.OPAQUE, new IntPtr(2));
            res = H5T.set_tag(attributeTypeId, "Opaque Test Tag");

            var attributeId = H5A.create(groupId, "opaque", attributeTypeId, spaceId);

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, attributeTypeId, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeId);
            res = H5A.close(attributeId);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddArrayAttribute(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "array");
            var spaceId = H5S.create_simple(2, new ulong[] { 2, 3 }, new ulong[] { 2, 3 });

            var attributeData = TestData.ArrayData;
            var attributeTypeId = H5T.array_create(H5T.NATIVE_INT32, 2, new ulong[] { 4, 5 });
            var attributeId = H5A.create(groupId, "array", attributeTypeId, spaceId);

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, attributeTypeId, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeId);
            res = H5A.close(attributeId);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddReferenceAttribute(long fileId)
        {
            long res;

            var length = (ulong)TestData.DatasetNumericalData.Count;
            var groupId = H5G.create(fileId, "reference");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });

            TestUtils.AddNumericalDatasets(fileId);

            var attributeData = new ulong[length];

            fixed (ulong* ptr = attributeData)
            {
                var referenceGroupId = H5G.open(fileId, "numerical");

                for (ulong i = 0; i < length; i++)
                {
                    res = H5R.create(new IntPtr(ptr + i), referenceGroupId, $"D{i + 1}", H5R.type_t.OBJECT, -1);
                }

                var attributeId = H5A.create(groupId, "reference", H5T.STD_REF_OBJ, spaceId);
                res = H5A.write(attributeId, H5T.STD_REF_OBJ, new IntPtr(ptr));

                res = H5A.close(attributeId);
                res = H5G.close(referenceGroupId);
            }

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddStructAttributes(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "struct");

            // "extendible contiguous non-external dataset not allowed"
            var spaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 2, 2, 3 });

            // non-nullable struct
            var attributeTypeId = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));
            var attributeId = H5A.create(groupId, "nonnullable", attributeTypeId, spaceId);
            var attributeData = TestData.NonNullableStructData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, attributeTypeId, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeId);
            res = H5A.close(attributeId);

            // nullable struct
            var attributeTypeIdNullable = TestUtils.GetHdfTypeIdFromType(typeof(TestStructString));
            var attributeIdNullable = H5A.create(groupId, "nullable", attributeTypeIdNullable, spaceId);
            var attributeDataNullable = TestData.StringStructData;

            // There is also Unsafe.SizeOf<T>() to calculate managed size instead of native size.
            // Is only relevant when Marshal.XX methods are replaced by other code.
            var elementSize = Marshal.SizeOf<TestStructString>();
            var totalByteLength = elementSize * attributeDataNullable.Length;
            var attributeDataNullablePtr = Marshal.AllocHGlobal(totalByteLength);
            var counter = 0;

            attributeDataNullable.Cast<ValueType>().ToList().ForEach(x =>
            {
                var sourcePtr = Marshal.AllocHGlobal(elementSize);
                Marshal.StructureToPtr(x, sourcePtr, false);

                var source = new Span<byte>(sourcePtr.ToPointer(), elementSize);
                var target = new Span<byte>(IntPtr.Add(attributeDataNullablePtr, elementSize * counter).ToPointer(), elementSize);

                source.CopyTo(target);
                counter++;
                Marshal.FreeHGlobal(sourcePtr);
            });

            H5A.write(attributeIdNullable, attributeTypeIdNullable, attributeDataNullablePtr);
            Marshal.FreeHGlobal(attributeDataNullablePtr);

            res = H5T.close(attributeTypeIdNullable);
            res = H5A.close(attributeIdNullable);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddStringAttributes(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "string");

            // "extendible contiguous non-external dataset not allowed"
            var spaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 2, 2, 3 });

            // fixed length string attribute (ASCII)
            var attributeTypeIdFixed = H5T.copy(H5T.C_S1);
            res = H5T.set_size(attributeTypeIdFixed, new IntPtr(2));
            res = H5T.set_cset(attributeTypeIdFixed, H5T.cset_t.ASCII);

            var attributeIdFixed = H5A.create(groupId, "fixed", attributeTypeIdFixed, spaceId);
            var attributeDataFixed = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var attributeDataFixedChar = attributeDataFixed
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            fixed (void* ptr = attributeDataFixedChar)
            {
                res = H5A.write(attributeIdFixed, attributeTypeIdFixed, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeIdFixed);
            res = H5A.close(attributeIdFixed);

            // variable length string attribute (ASCII)
            var attributeTypeIdVar = H5T.copy(H5T.C_S1);
            res = H5T.set_size(attributeTypeIdVar, H5T.VARIABLE);
            res = H5T.set_cset(attributeTypeIdVar, H5T.cset_t.ASCII);

            var attributeIdVar = H5A.create(groupId, "variable", attributeTypeIdVar, spaceId);
            var attributeDataVar = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var attributeDataVarIntPtr = attributeDataVar.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = attributeDataVarIntPtr)
            {
                res = H5A.write(attributeIdVar, attributeTypeIdVar, new IntPtr(ptr));
            }

            foreach (var ptr in attributeDataVarIntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(attributeTypeIdVar);
            res = H5A.close(attributeIdVar);

            // variable length string attribute (UTF8)
            var attributeTypeIdVarUTF8 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(attributeTypeIdVarUTF8, H5T.VARIABLE);
            res = H5T.set_cset(attributeTypeIdVarUTF8, H5T.cset_t.UTF8);

            var attributeIdVarUTF8 = H5A.create(groupId, "variableUTF8", attributeTypeIdVarUTF8, spaceId);
            var attributeDataVarUTF8 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" };
            var attributeDataVarUTF8IntPtr = attributeDataVarUTF8.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = attributeDataVarUTF8IntPtr)
            {
                res = H5A.write(attributeIdVarUTF8, attributeTypeIdVarUTF8, new IntPtr(ptr));
            }

            foreach (var ptr in attributeDataVarUTF8IntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(attributeTypeIdVarUTF8);
            res = H5A.close(attributeIdVarUTF8);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddMassAttributes(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "mass_attributes");

            for (int i = 0; i < 1000; i++)
            {
                var spaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 3, 3, 4 });
                var attributeTypeId = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));

                long attributeId;

                if (i == 450)
                {
                    var acpl_id = H5P.create(H5P.ATTRIBUTE_CREATE);
                    var bytes = Encoding.UTF8.GetBytes("字形碼 / 字形码, Zìxíngmǎ");
                    res = H5P.set_char_encoding(acpl_id, H5T.cset_t.UTF8);
                    attributeId = H5A.create(groupId, bytes, attributeTypeId, spaceId, acpl_id);
                }
                else
                {
                    var name = $"mass_{i.ToString("D4")}";
                    attributeId = H5A.create(groupId, name, attributeTypeId, spaceId);
                }

                var attributeData = TestData.NonNullableStructData;

                fixed (void* ptr = attributeData)
                {
                    res = H5A.write(attributeId, attributeTypeId, new IntPtr(ptr));
                }

                res = H5S.close(spaceId);
                res = H5T.close(attributeTypeId);
                res = H5A.close(attributeId);
            }

            res = H5G.close(groupId);
        }

        public static unsafe void AddSmallAttribute(long fileId)
        {
            long res;

            var length = (ulong)TestData.SmallData.Length;
            var groupId = H5G.create(fileId, "small");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var attributeId = H5A.create(groupId, "small", H5T.NATIVE_INT32, spaceId);
            var attributeData = TestData.SmallData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.NATIVE_INT32, new IntPtr(ptr));
            }

            res = H5S.close(spaceId);
            res = H5A.close(attributeId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddHugeAttribute(long fileId, H5F.libver_t version)
        {
            long res;

            var length = version switch
            {
                H5F.libver_t.EARLIEST => 16368UL, // max 64 kb in object header
                _ => (ulong)TestData.HugeData.Length,
            };

            var groupId = H5G.create(fileId, "huge");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var attributeId = H5A.create(groupId, "huge", H5T.NATIVE_INT32, spaceId);
            var attributeData = TestData.HugeData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.NATIVE_INT32, new IntPtr(ptr));
            }

            res = H5S.close(spaceId);
            res = H5A.close(attributeId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddTinyAttribute(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "tiny");
            var spaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var attributeId = H5A.create(groupId, "tiny", H5T.NATIVE_UINT8, spaceId);
            var attributeData = TestData.TinyData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.NATIVE_UINT8, new IntPtr(ptr));
            }

            res = H5S.close(spaceId);
            res = H5A.close(attributeId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddLinks(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "links");

            var hardLinkId1 = H5G.create(groupId, "hard_link_1");
            res = H5L.create_hard(groupId, "hard_link_1", groupId, "hard_link_2");
            res = H5L.create_soft("hard_link_2", groupId, "soft_link_1");
            res = H5L.create_soft("/links/soft_link_1", groupId, "soft_link_2");

            var spaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId = H5D.create(hardLinkId1, "dataset", H5T.NATIVE_INT, spaceId);

            res = H5L.create_soft("/links/soft_link_2/dataset", groupId, "dataset");

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);

            res = H5G.close(groupId);
            res = H5G.close(hardLinkId1);
        }

        public static unsafe void AddExternalFileLink(long fileId, string filePath)
        {
            long res;

            // create external link
            var groupId = H5G.create(fileId, "links");
            res = H5L.create_external(filePath, "/external/group", groupId, "external_link");
            res = H5G.close(groupId);
        }

        public static unsafe void AddNumericalDatasets(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "numerical");

            // "extendible contiguous non-external dataset not allowed"
            var spaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 2, 2, 3 });

            foreach (var entry in TestData.DatasetNumericalData)
            {
                var datasetData = (Array)entry[1];
                var type = datasetData
                    .GetType()
                    .GetElementType();

                var typeId = TestUtils.GetHdfTypeIdFromType(type);
                var datasetId = H5D.create(groupId, (string)entry[0], typeId, spaceId);

                if (type == typeof(TestEnum))
                {
                    datasetData = datasetData
                        .OfType<object>()
                        .Select(value => (short)value)
                        .ToArray();
                }

                var handle = GCHandle.Alloc(datasetData, GCHandleType.Pinned);
                var ptr = handle.AddrOfPinnedObject().ToPointer();
                res = H5D.write(datasetId, typeId, spaceId, H5S.ALL, 0, new IntPtr(ptr));

                handle.Free();

                res = H5D.close(datasetId);
            }

            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddStructDatasets(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "struct");

            // "extendible contiguous non-external dataset not allowed"
            var spaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 2, 2, 3 });

            // non-nullable struct
            var datasetTypeId = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));
            var datasetId = H5D.create(groupId, "nonnullable", datasetTypeId, spaceId);
            var datasetData = TestData.NonNullableStructData;

            fixed (void* ptr = datasetData)
            {
                res = H5D.write(datasetId, datasetTypeId, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5T.close(datasetTypeId);
            res = H5D.close(datasetId);

            // nullable struct
            var datasetTypeIdNullable = TestUtils.GetHdfTypeIdFromType(typeof(TestStructString));
            var datasetIdNullable = H5D.create(groupId, "nullable", datasetTypeIdNullable, spaceId);
            var datasetDataNullable = TestData.StringStructData;

            // There is also Unsafe.SizeOf<T>() to calculate managed size instead of native size.
            // Is only relevant when Marshal.XX methods are replaced by other code.
            var elementSize = Marshal.SizeOf<TestStructString>();
            var totalByteLength = elementSize * datasetDataNullable.Length;
            var datasetDataNullablePtr = Marshal.AllocHGlobal(totalByteLength);
            var counter = 0;

            datasetDataNullable.Cast<ValueType>().ToList().ForEach(x =>
            {
                var sourcePtr = Marshal.AllocHGlobal(elementSize);
                Marshal.StructureToPtr(x, sourcePtr, false);

                var source = new Span<byte>(sourcePtr.ToPointer(), elementSize);
                var target = new Span<byte>(IntPtr.Add(datasetDataNullablePtr, elementSize * counter).ToPointer(), elementSize);

                source.CopyTo(target);
                counter++;
                Marshal.FreeHGlobal(sourcePtr);
            });

            H5D.write(datasetIdNullable, datasetTypeIdNullable, spaceId, H5S.ALL, 0, datasetDataNullablePtr);
            Marshal.FreeHGlobal(datasetDataNullablePtr);

            res = H5T.close(datasetTypeIdNullable);
            res = H5D.close(datasetIdNullable);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddStringDatasets(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "string");

            // "extendible contiguous non-external dataset not allowed"
            var spaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 2, 2, 3 });

            // fixed length string dataset (ASCII)
            var datasetTypeIdFixed = H5T.copy(H5T.C_S1);
            res = H5T.set_size(datasetTypeIdFixed, new IntPtr(2));
            res = H5T.set_cset(datasetTypeIdFixed, H5T.cset_t.ASCII);

            var datasetIdFixed = H5D.create(groupId, "fixed", datasetTypeIdFixed, spaceId);
            var datasetDataFixed = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var datasetDataFixedChar = datasetDataFixed
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            fixed (void* ptr = datasetDataFixedChar)
            {
                res = H5D.write(datasetIdFixed, datasetTypeIdFixed, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5T.close(datasetTypeIdFixed);
            res = H5D.close(datasetIdFixed);

            // variable length string dataset (ASCII)
            var datasetTypeIdVar = H5T.copy(H5T.C_S1);
            res = H5T.set_size(datasetTypeIdVar, H5T.VARIABLE);
            res = H5T.set_cset(datasetTypeIdVar, H5T.cset_t.ASCII);

            var datasetIdVar = H5D.create(groupId, "variable", datasetTypeIdVar, spaceId);
            var datasetDataVar = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var datasetDataVarIntPtr = datasetDataVar.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = datasetDataVarIntPtr)
            {
                res = H5D.write(datasetIdVar, datasetTypeIdVar, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            foreach (var ptr in datasetDataVarIntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(datasetTypeIdVar);
            res = H5D.close(datasetIdVar);

            // variable length string dataset (UTF8)
            var datasetTypeIdVarUTF8 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(datasetTypeIdVarUTF8, H5T.VARIABLE);
            res = H5T.set_cset(datasetTypeIdVarUTF8, H5T.cset_t.UTF8);

            var datasetIdVarUTF8 = H5D.create(groupId, "variableUTF8", datasetTypeIdVarUTF8, spaceId);
            var datasetDataVarUTF8 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" };
            var datasetDataVarUTF8IntPtr = datasetDataVarUTF8.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = datasetDataVarUTF8IntPtr)
            {
                res = H5D.write(datasetIdVarUTF8, datasetTypeIdVarUTF8, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            foreach (var ptr in datasetDataVarUTF8IntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(datasetTypeIdVarUTF8);
            res = H5D.close(datasetIdVarUTF8);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddBitFieldDataset(long fileId)
        {
            long res;

            var length = (ulong)TestData.BitfieldData.Length;
            var groupId = H5G.create(fileId, "bitfield");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });

            var datasetData = TestData.BitfieldData;
            var datasetId = H5D.create(groupId, "bitfield", H5T.STD_B16LE, spaceId);

            fixed (void* ptr = datasetData)
            {
                res = H5D.write(datasetId, H5T.STD_B16LE, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddOpaqueDataset(long fileId)
        {
            long res;

            var length = (ulong)TestData.SmallData.Length * 2;
            var groupId = H5G.create(fileId, "opaque");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });

            var datasetData = TestData.SmallData;
            var datasetTypeId = H5T.create(H5T.class_t.OPAQUE, new IntPtr(2));
            res = H5T.set_tag(datasetTypeId, "Opaque Test Tag");

            var datasetId = H5D.create(groupId, "opaque", datasetTypeId, spaceId);

            fixed (void* ptr = datasetData)
            {
                res = H5D.write(datasetId, datasetTypeId, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5T.close(datasetTypeId);
            res = H5D.close(datasetId);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddArrayDataset(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "array");
            var spaceId = H5S.create_simple(2, new ulong[] { 2, 3 }, new ulong[] { 2, 3 });

            var attributeData = TestData.ArrayData;
            var attributeTypeId = H5T.array_create(H5T.NATIVE_INT32, 2, new ulong[] { 4, 5 });
            var attributeId = H5D.create(groupId, "array", attributeTypeId, spaceId);

            fixed (void* ptr = attributeData)
            {
                res = H5D.write(attributeId, attributeTypeId, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeId);
            res = H5D.close(attributeId);

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddReferenceDataset(long fileId)
        {
            long res;

            var length = (ulong)TestData.DatasetNumericalData.Count;
            var groupId = H5G.create(fileId, "reference");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });

            TestUtils.AddNumericalDatasets(fileId);

            var datasetData = new ulong[length];

            fixed (ulong* ptr = datasetData)
            {
                var referenceGroupId = H5G.open(fileId, "numerical");

                for (ulong i = 0; i < length; i++)
                {
                    res = H5R.create(new IntPtr(ptr + i), referenceGroupId, $"D{i + 1}", H5R.type_t.OBJECT, -1);
                }

                var datasetId = H5D.create(groupId, "reference", H5T.STD_REF_OBJ, spaceId);
                res = H5D.write(datasetId, H5T.STD_REF_OBJ, spaceId, H5S.ALL, 0, new IntPtr(ptr));

                res = H5D.close(datasetId);
                res = H5G.close(referenceGroupId);
            }

            //
            res = H5S.close(spaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddCompactDataset(long fileId)
        {
            long res;

            var length = (ulong)TestData.SmallData.Length;
            var groupId = H5G.create(fileId, "compact");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_layout(dcpl_id, H5D.layout_t.COMPACT);
            var datasetId = H5D.create(groupId, "compact", H5T.NATIVE_INT32, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.SmallData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT32, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddContiguousDataset(long fileId)
        {
            long res;

            var length = (ulong)TestData.HugeData.Length;
            var groupId = H5G.create(fileId, "contiguous");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var datasetId = H5D.create(groupId, "contiguous", H5T.NATIVE_INT, spaceId);
            var dataset = TestData.HugeData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddContiguousDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length;
            var groupId = H5G.create(fileId, "fillvalue");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            res = H5P.set_layout(dcpl_id, H5D.layout_t.CONTIGUOUS);

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            var datasetId = H5D.create(groupId, $"{LayoutClass.Contiguous}", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Legacy(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length;
            var groupId = H5G.create(fileId, "fillvalue");
            var spaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            res = H5P.set_chunk(dcpl_id, 1, new ulong[] { 1000 });

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            var datasetId = H5D.create(groupId, $"{LayoutClass.Chunked}", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);

            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Single_Chunk(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { length, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_single_chunk", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Implicit(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.EARLY);

            var datasetId = H5D.create(groupId, "chunked_implicit", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_fixed_array", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array_Paged(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_fixed_array_paged", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Elements(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_extensible_array_elements", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Data_Blocks(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 100, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_extensible_array_data_blocks", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Secondary_Blocks(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 3, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_extensible_array_secondary_blocks", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_BTree2(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, H5S.UNLIMITED });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_btree2", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_Shuffle(long fileId, int bytesOfType, int length, Span<byte> dataset)
        {
            long res;

            var groupId = H5G.create(fileId, "filtered");
            var spaceId = H5S.create_simple(1, new ulong[] { (ulong)length }, new ulong[] { (ulong)length });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 1, new ulong[] { (ulong)length });
            res = H5P.set_shuffle(dcpl_id);

            var type = bytesOfType switch
            {
                1 => H5T.NATIVE_UINT8,
                2 => H5T.NATIVE_UINT16,
                4 => H5T.NATIVE_UINT32,
                8 => H5T.NATIVE_UINT64,
                _ => throw new Exception($"The value '{bytesOfType}' of the 'bytesOfType' parameter is not within the valid range.")
            };

            var datasetId = H5D.create(groupId, $"shuffle_{bytesOfType}", type, spaceId, dcpl_id: dcpl_id);

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, type, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_ZLib(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "filtered");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_filter(dcpl_id, H5Z.filter_t.DEFLATE, 0, new IntPtr(1), new uint[] { 5 } /* compression level */);

            var datasetId = H5D.create(groupId, "deflate", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_Fletcher(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "filtered");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_fletcher32(dcpl_id);

            var datasetId = H5D.create(groupId, "fletcher", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_Multi(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "filtered");
            var spaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_fletcher32(dcpl_id);
            res = H5P.set_shuffle(dcpl_id);
            res = H5P.set_deflate(dcpl_id, level: 5);

            var datasetId = H5D.create(groupId, "multi", H5T.NATIVE_INT, spaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, spaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5P.close(dcpl_id);
            res = H5S.close(spaceId);
            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static bool ReadAndCompare<T>(H5Dataset dataset, T[] expected) where T : struct
        {
            var actual = dataset.Read<T>();
            return actual.SequenceEqual(expected);
        }

        private static long GetHdfTypeIdFromType(Type type)
        {
            var elementType = type.IsArray ? type.GetElementType() : type;

            if (elementType == typeof(bool))
                return H5T.NATIVE_UINT8;

            else if (elementType == typeof(byte))
                return H5T.NATIVE_UINT8;

            else if (elementType == typeof(sbyte))
                return H5T.NATIVE_INT8;

            else if (elementType == typeof(ushort))
                return H5T.NATIVE_UINT16;

            else if (elementType == typeof(short))
                return H5T.NATIVE_INT16;

            else if (elementType == typeof(uint))
                return H5T.NATIVE_UINT32;

            else if (elementType == typeof(int))
                return H5T.NATIVE_INT32;

            else if (elementType == typeof(ulong))
                return H5T.NATIVE_UINT64;

            else if (elementType == typeof(long))
                return H5T.NATIVE_INT64;

            else if (elementType == typeof(float))
                return H5T.NATIVE_FLOAT;

            else if (elementType == typeof(double))
                return H5T.NATIVE_DOUBLE;

            // issues: https://en.wikipedia.org/wiki/Long_double
            //else if (elementType == typeof(decimal))
            //    return H5T.NATIVE_LDOUBLE;

            else if (elementType.IsEnum)
            {
                var baseTypeId = TestUtils.GetHdfTypeIdFromType(Enum.GetUnderlyingType(elementType));
                var typeId = H5T.enum_create(baseTypeId);

                foreach (var value in Enum.GetValues(type))
                {
                    var value_converted = Convert.ToInt64(value);
                    var name = Enum.GetName(type, value_converted);

                    var handle = GCHandle.Alloc(value_converted, GCHandleType.Pinned);
                    H5T.enum_insert(typeId, name, handle.AddrOfPinnedObject());
                }

                return typeId;
            }

            else if (elementType == typeof(string) || elementType == typeof(IntPtr))
            {
                var typeId = H5T.copy(H5T.C_S1);

                H5T.set_size(typeId, H5T.VARIABLE);
                H5T.set_cset(typeId, H5T.cset_t.UTF8);

                return typeId;
            }
            else if (elementType.IsValueType && !elementType.IsPrimitive)
            {
                var typeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(Marshal.SizeOf(elementType)));

                foreach (var fieldInfo in elementType.GetFields())
                {
                    var fieldType = TestUtils.GetHdfTypeIdFromType(fieldInfo.FieldType);
                    var attribute = fieldInfo.GetCustomAttribute<H5NameAttribute>(true);
                    var hdfFieldName = attribute != null ? attribute.Name : fieldInfo.Name;

                    H5T.insert(typeId, hdfFieldName, Marshal.OffsetOf(elementType, fieldInfo.Name), fieldType);

                    if (H5I.is_valid(fieldType) > 0)
                        H5T.close(fieldType);
                }

                return typeId;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}