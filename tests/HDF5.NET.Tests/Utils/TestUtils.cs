using HDF.PInvoke;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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

        public static unsafe void AddTypedAttributes(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "typed");
            var attributeSpaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 3, 3, 4 });

            // numeric attributes
            foreach (var entry in TestData.AttributeNumericalTestData)
            {
                var attributeData = (Array)entry[1];
                var type = attributeData
                    .GetType()
                    .GetElementType();

                var typeId = TestUtils.GetHdfTypeIdFromType(type);
                var attributeId = H5A.create(groupId, (string)entry[0], typeId, attributeSpaceId);
                var handle = GCHandle.Alloc(attributeData, GCHandleType.Pinned);

                var ptr = handle.AddrOfPinnedObject().ToPointer();
                res = H5A.write(attributeId, typeId, new IntPtr(ptr));

                handle.Free();

                res = H5A.close(attributeId);
            }
           
            // fixed length string attribute (ASCII)
            var attributeTypeId11 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(attributeTypeId11, new IntPtr(2));
            res = H5T.set_cset(attributeTypeId11, H5T.cset_t.ASCII);

            var attributeId11 = H5A.create(groupId, "A11", attributeTypeId11, attributeSpaceId);
            var attributeData11 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var attributeData11Char = attributeData11
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            fixed (void* ptr = attributeData11Char)
            {
                res = H5A.write(attributeId11, attributeTypeId11, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeId11);
            res = H5A.close(attributeId11);

            // variable length string attribute (ASCII)
            var attributeTypeId12 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(attributeTypeId12, H5T.VARIABLE);
            res = H5T.set_cset(attributeTypeId12, H5T.cset_t.ASCII);

            var attributeId12 = H5A.create(groupId, "A12", attributeTypeId12, attributeSpaceId);
            var attributeData12 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var attributeData12IntPtr = attributeData12.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = attributeData12IntPtr)
            {
                res = H5A.write(attributeId12, attributeTypeId12, new IntPtr(ptr));
            }

            foreach (var ptr in attributeData12IntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(attributeTypeId12);
            res = H5A.close(attributeId12);

            // variable length string attribute (UTF8)
            var attributeTypeId13 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(attributeTypeId13, H5T.VARIABLE);
            res = H5T.set_cset(attributeTypeId13, H5T.cset_t.UTF8);

            var attributeId13 = H5A.create(groupId, "A13", attributeTypeId13, attributeSpaceId);
            var attributeData13 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" };
            var attributeData13IntPtr = attributeData13.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = attributeData13IntPtr)
            {
                res = H5A.write(attributeId13, attributeTypeId13, new IntPtr(ptr));
            }

            foreach (var ptr in attributeData13IntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(attributeTypeId13);
            res = H5A.close(attributeId13);

            // non-nullable struct
            var attributeTypeId14 = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));
            var attributeId14 = H5A.create(groupId, "A14", attributeTypeId14, attributeSpaceId);
            var attributeData14 = TestData.NonNullableTestStructData;

            fixed (void* ptr = attributeData14)
            {
                res = H5A.write(attributeId14, attributeTypeId14, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeId14);
            res = H5A.close(attributeId14);

            // nullable struct
            var attributeTypeId15 = TestUtils.GetHdfTypeIdFromType(typeof(TestStructString));
            var attributeId15 = H5A.create(groupId, "A15", attributeTypeId15, attributeSpaceId);
            var attributeData15 = TestData.StringTestStructData;

            // There is also Unsafe.SizeOf<T>() to calculate managed size instead of native size.
            // Is only relevant when Marshal.XX methods are replaced by other code.
            var elementSize = Marshal.SizeOf<TestStructString>();
            var totalByteLength = elementSize * attributeData15.Length;
            var attributeData15Ptr = Marshal.AllocHGlobal(totalByteLength);
            var counter = 0;

            attributeData15.Cast<ValueType>().ToList().ForEach(x =>
            {
                var sourcePtr = Marshal.AllocHGlobal(elementSize);
                Marshal.StructureToPtr(x, sourcePtr, false);

                var source = new Span<byte>(sourcePtr.ToPointer(), elementSize);
                var target = new Span<byte>(IntPtr.Add(attributeData15Ptr, elementSize * counter).ToPointer(), elementSize);

                source.CopyTo(target);
                counter++;
                Marshal.FreeHGlobal(sourcePtr);
            });

            H5A.write(attributeId15, attributeTypeId15, attributeData15Ptr);
            Marshal.FreeHGlobal(attributeData15Ptr);

            res = H5T.close(attributeTypeId15);
            res = H5A.close(attributeId15);

            //
            res = H5S.close(attributeSpaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddMassAttributes(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "mass_attributes");

            for (int i = 0; i < 1000; i++)
            {
                var attributeSpaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 3, 3, 4 });
                var attributeTypeId = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));

                long attributeId;

                if (i == 450)
                {
                    var acpl_id = H5P.create(H5P.ATTRIBUTE_CREATE);
                    var bytes = Encoding.UTF8.GetBytes("字形碼 / 字形码, Zìxíngmǎ");
                    res = H5P.set_char_encoding(acpl_id, H5T.cset_t.UTF8);
                    attributeId = H5A.create(groupId, bytes, attributeTypeId, attributeSpaceId, acpl_id);
                }
                else
                {
                    var name = $"mass_{i.ToString("D4")}";
                    attributeId = H5A.create(groupId, name, attributeTypeId, attributeSpaceId);
                }

                var attributeData = TestData.NonNullableTestStructData;

                fixed (void* ptr = attributeData)
                {
                    res = H5A.write(attributeId, attributeTypeId, new IntPtr(ptr));
                }

                res = H5S.close(attributeSpaceId);
                res = H5T.close(attributeTypeId);
                res = H5A.close(attributeId);
            }

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

            var groupId = H5G.create(fileId, "large");
            var attributeSpaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var attributeId = H5A.create(groupId, "large", H5T.NATIVE_INT32, attributeSpaceId);
            var attributeData = TestData.HugeData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.NATIVE_INT32, new IntPtr(ptr));
            }

            res = H5S.close(attributeSpaceId);
            res = H5A.close(attributeId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddTinyAttribute(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "tiny");
            var attributeSpaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var attributeId = H5A.create(groupId, "tiny", H5T.NATIVE_UINT8, attributeSpaceId);
            var attributeData = TestData.TinyData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.NATIVE_UINT8, new IntPtr(ptr));
            }

            res = H5S.close(attributeSpaceId);
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

        public static unsafe void AddTypedDatasets(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "typed");

            // "extendible contiguous non-external dataset not allowed"
            var datasetSpaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 2, 2, 3 });

            // numeric datasets
            foreach (var entry in TestData.DatasetNumericalTestData)
            {
                var datasetData = (Array)entry[1];
                var type = datasetData
                    .GetType()
                    .GetElementType();

                var typeId = TestUtils.GetHdfTypeIdFromType(type);
                var datasetId = H5D.create(groupId, (string)entry[0], typeId, datasetSpaceId);
                var handle = GCHandle.Alloc(datasetData, GCHandleType.Pinned);

                var ptr = handle.AddrOfPinnedObject().ToPointer();
                res = H5D.write(datasetId, typeId, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));

                handle.Free();

                res = H5D.close(datasetId);
            }

            // fixed length string dataset (ASCII)
            var datasetTypeId11 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(datasetTypeId11, new IntPtr(2));
            res = H5T.set_cset(datasetTypeId11, H5T.cset_t.ASCII);

            var datasetId11 = H5D.create(groupId, "D11", datasetTypeId11, datasetSpaceId);
            var datasetData11 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var datasetData11Char = datasetData11
                .SelectMany(value => Encoding.ASCII.GetBytes(value))
                .ToArray();

            fixed (void* ptr = datasetData11Char)
            {
                res = H5D.write(datasetId11, datasetTypeId11, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5T.close(datasetTypeId11);
            res = H5D.close(datasetId11);

            // variable length string dataset (ASCII)
            var datasetTypeId12 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(datasetTypeId12, H5T.VARIABLE);
            res = H5T.set_cset(datasetTypeId12, H5T.cset_t.ASCII);

            var datasetId12 = H5D.create(groupId, "D12", datasetTypeId12, datasetSpaceId);
            var datasetData12 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "AA", "ZZ", "!!" };
            var datasetData12IntPtr = datasetData12.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = datasetData12IntPtr)
            {
                res = H5D.write(datasetId12, datasetTypeId12, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            foreach (var ptr in datasetData12IntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(datasetTypeId12);
            res = H5D.close(datasetId12);

            // variable length string dataset (UTF8)
            var datasetTypeId13 = H5T.copy(H5T.C_S1);
            res = H5T.set_size(datasetTypeId13, H5T.VARIABLE);
            res = H5T.set_cset(datasetTypeId13, H5T.cset_t.UTF8);

            var datasetId13 = H5D.create(groupId, "D13", datasetTypeId13, datasetSpaceId);
            var datasetData13 = new string[] { "00", "11", "22", "33", "44", "55", "66", "77", "  ", "ÄÄ", "的的", "!!" };
            var datasetData13IntPtr = datasetData13.Select(x => Marshal.StringToCoTaskMemUTF8(x)).ToArray();

            fixed (void* ptr = datasetData13IntPtr)
            {
                res = H5D.write(datasetId13, datasetTypeId13, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            foreach (var ptr in datasetData13IntPtr)
            {
                Marshal.FreeCoTaskMem(ptr);
            }

            res = H5T.close(datasetTypeId13);
            res = H5D.close(datasetId13);

            // non-nullable struct
            var datasetTypeId14 = TestUtils.GetHdfTypeIdFromType(typeof(TestStructL1));
            var datasetId14 = H5D.create(groupId, "D14", datasetTypeId14, datasetSpaceId);
            var datasetData14 = TestData.NonNullableTestStructData;

            fixed (void* ptr = datasetData14)
            {
                res = H5D.write(datasetId14, datasetTypeId14, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5T.close(datasetTypeId14);
            res = H5D.close(datasetId14);

            // nullable struct
            var datasetTypeId15 = TestUtils.GetHdfTypeIdFromType(typeof(TestStructString));
            var datasetId15 = H5D.create(groupId, "D15", datasetTypeId15, datasetSpaceId);
            var datasetData15 = TestData.StringTestStructData;

            // There is also Unsafe.SizeOf<T>() to calculate managed size instead of native size.
            // Is only relevant when Marshal.XX methods are replaced by other code.
            var elementSize = Marshal.SizeOf<TestStructString>();
            var totalByteLength = elementSize * datasetData15.Length;
            var datasetData15Ptr = Marshal.AllocHGlobal(totalByteLength);
            var counter = 0;

            datasetData15.Cast<ValueType>().ToList().ForEach(x =>
            {
                var sourcePtr = Marshal.AllocHGlobal(elementSize);
                Marshal.StructureToPtr(x, sourcePtr, false);

                var source = new Span<byte>(sourcePtr.ToPointer(), elementSize);
                var target = new Span<byte>(IntPtr.Add(datasetData15Ptr, elementSize * counter).ToPointer(), elementSize);

                source.CopyTo(target);
                counter++;
                Marshal.FreeHGlobal(sourcePtr);
            });

            H5D.write(datasetId15, datasetTypeId15, datasetSpaceId, H5S.ALL, 0, datasetData15Ptr);
            Marshal.FreeHGlobal(datasetData15Ptr);

            res = H5T.close(datasetTypeId15);
            res = H5D.close(datasetId15);

            //
            res = H5S.close(datasetSpaceId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddCompactDataset(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "compact");
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_layout(dcpl_id, H5D.layout_t.COMPACT);
            var datasetId = H5D.create(groupId, "compact", H5T.NATIVE_UINT8, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.TinyData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_UINT8, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddContiguousDataset(long fileId)
        {
            long res;

            var length = (ulong)TestData.HugeData.Length;
            var groupId = H5G.create(fileId, "contiguous");         
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var datasetId = H5D.create(groupId, "contiguous", H5T.NATIVE_INT, datasetSpaceId);
            var dataset = TestData.HugeData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddContiguousDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length;
            var groupId = H5G.create(fileId, "fillvalue");
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            res = H5P.set_layout(dcpl_id, H5D.layout_t.CONTIGUOUS);

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            var datasetId = H5D.create(groupId, $"{LayoutClass.Contiguous}", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Legacy(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDatasetWithFillValueAndAllocationLate(long fileId, int fillValue)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length;
            var groupId = H5G.create(fileId, "fillvalue");
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);

            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.LATE);
            res = H5P.set_chunk(dcpl_id, 1, new ulong[] { 1000 });

            var handle = GCHandle.Alloc(BitConverter.GetBytes(fillValue), GCHandleType.Pinned);
            H5P.set_fill_value(dcpl_id, H5T.NATIVE_INT, handle.AddrOfPinnedObject());
            handle.Free();

            var datasetId = H5D.create(groupId, $"{LayoutClass.Chunked}", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Single_Chunk(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { length, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_single_chunk", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Implicit(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_alloc_time(dcpl_id, H5D.alloc_time_t.EARLY);

            var datasetId = H5D.create(groupId, "chunked_implicit", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_fixed_array", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Fixed_Array_Paged(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_fixed_array_paged", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Elements(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_extensible_array_elements", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Data_Blocks(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 100, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_extensible_array_data_blocks", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_Extensible_Array_Secondary_Blocks(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 3, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_extensible_array_secondary_blocks", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddChunkedDataset_BTree2(long fileId, bool withShuffle)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { H5S.UNLIMITED, H5S.UNLIMITED });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });

            if (withShuffle)
                res = H5P.set_shuffle(dcpl_id);

            var datasetId = H5D.create(groupId, "chunked_btree2", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_Shuffle(long fileId, int bytesOfType, int length, Span<byte> dataset)
        {
            long res;

            var groupId = H5G.create(fileId, "filtered");
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { (ulong)length }, new ulong[] { (ulong)length });
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

            var datasetId = H5D.create(groupId, $"shuffle_{bytesOfType}", type, datasetSpaceId, dcpl_id: dcpl_id);

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, type, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_ZLib(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "filtered");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_filter(dcpl_id, H5Z.filter_t.DEFLATE, 0, new IntPtr(1), new uint[] { 5 } /* compression level */);

            var datasetId = H5D.create(groupId, "deflate", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_Fletcher(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "filtered");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_fletcher32(dcpl_id);

            var datasetId = H5D.create(groupId, "fletcher", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        public static unsafe void AddFilteredDataset_Multi(long fileId)
        {
            long res;

            var length = (ulong)TestData.MediumData.Length / 4;
            var groupId = H5G.create(fileId, "filtered");
            var datasetSpaceId = H5S.create_simple(2, new ulong[] { length, 4 }, new ulong[] { length, 4 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 2, new ulong[] { 1000, 4 });
            res = H5P.set_fletcher32(dcpl_id);
            res = H5P.set_shuffle(dcpl_id);
            res = H5P.set_deflate(dcpl_id, level: 5);

            var datasetId = H5D.create(groupId, "multi", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var dataset = TestData.MediumData;

            fixed (void* ptr = dataset)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
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
                return TestUtils.GetHdfTypeIdFromType(Enum.GetUnderlyingType(elementType));

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