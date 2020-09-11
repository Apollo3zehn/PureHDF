using HDF.PInvoke;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace HDF5.NET.Tests
{
    public class TestUtils
    {
        private static TestStructL1 _nn_a = new TestStructL1()
        {
            ByteValue = 1,
            UShortValue = 2,
            UIntValue = 3,
            ULongValue = 4,
            L2Struct1 = new TestStructL2()
            {
                ByteValue = 99,
                UShortValue = 65535,
                EnumValue = TestEnum.b,
            },
            SByteValue = 5,
            ShortValue = -6,
            IntValue = 7,
            LongValue = -8,
            L2Struct2 = new TestStructL2()
            {
                ByteValue = 9,
                EnumValue = TestEnum.b,
            }
        };

        private static TestStructL1 _nn_b = new TestStructL1()
        {
            ByteValue = 2,
            UShortValue = 4,
            UIntValue = 6,
            ULongValue = 8,
            L2Struct1 = new TestStructL2()
            {
                ByteValue = 99,
                UShortValue = 65535,
                EnumValue = TestEnum.a,
            },
            SByteValue = -10,
            ShortValue = 12,
            IntValue = 14,
            LongValue = -16,
            L2Struct2 = new TestStructL2()
            {
                ByteValue = 18,
                UShortValue = 20,
                EnumValue = TestEnum.b,
            }
        };

        private static TestStructString _string_a = new TestStructString()
        {
            FloatValue = (float)1.299e9,
            StringValue1 = "Hello",
            StringValue2 = "World",
            ByteValue = 123,
            ShortValue = -15521,
            L2Struct = new TestStructL2()
            {
                ByteValue = 15,
                UShortValue = 20,
                EnumValue = TestEnum.a,
            }
        };

        private static TestStructString _string_b = new TestStructString()
        {
            FloatValue = (float)2.299e-9,
            StringValue1 = "Hello!",
            StringValue2 = "World!",
            ByteValue = 0,
            ShortValue = 15521,
            L2Struct = new TestStructL2()
            {
                ByteValue = 18,
                UShortValue = 21,
                EnumValue = TestEnum.b,
            }
        };

        static TestUtils()
        {
            TestUtils.NonNullableTestStructData = new TestStructL1[] { _nn_a, _nn_b, _nn_a, _nn_a, _nn_b, _nn_b, _nn_b, _nn_b, _nn_a, _nn_a, _nn_b, _nn_a };
            TestUtils.StringTestStructData = new TestStructString[] { _string_a, _string_b, _string_a, _string_a, _string_b, _string_b, _string_b, _string_b, _string_a, _string_a, _string_b, _string_a };
            TestUtils.HugeData = Enumerable.Range(0, 10_000_000).ToArray();
            TestUtils.TinyData = new byte[] { 99 };
        }

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

        public static TestStructL1[] NonNullableTestStructData { get; }

        public static TestStructString[] StringTestStructData { get; }

        public static int[] HugeData { get; }
        public static byte[] TinyData { get; }

        public static unsafe string PrepareTestFile(H5F.libver_t version,
                                                    bool withSimple = false,
                                                    bool withMassLinks = false,
                                                    bool withTypedAttributes = false,
                                                    bool withMassAttributes = false,
                                                    bool withHugeAttribute = false,
                                                    bool withTinyAttribute = false,
                                                    bool withLinks = false,
                                                    bool withCompactDataset = false,
                                                    bool withContiguousDataset = false,
                                                    bool withChunkedDataset = false)
        {
            var filePath = Path.GetTempFileName();
            long res;

            // file
            var faplId = H5P.create(H5P.FILE_ACCESS);
            res = H5P.set_libver_bounds(faplId, version, version);
            var fileId = H5F.create(filePath, H5F.ACC_TRUNC, 0, faplId);

            if (withSimple)
                TestUtils.AddSimple(fileId);

            if (withMassLinks)
                TestUtils.AddMassLinks(fileId);

            if (withTypedAttributes)
                TestUtils.AddTypedAttributes(fileId);

            if (withMassAttributes)
                TestUtils.AddMassAttributes(fileId);

            if (withHugeAttribute)
                TestUtils.AddHugeAttribute(fileId, version);

            if (withTinyAttribute)
                TestUtils.AddTinyAttribute(fileId);

            if (withLinks)
                TestUtils.AddLinks(fileId);

            if (withCompactDataset)
                TestUtils.AddCompactDataset(fileId);

            if (withContiguousDataset)
                TestUtils.AddContiguousDataset(fileId);

            if (withChunkedDataset)
                TestUtils.AddChunkedDataset(fileId);

            res = H5F.close(fileId);

            return filePath;
        }

        private static unsafe void AddSimple(long fileId)
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

        private static unsafe void AddMassLinks(long fileId)
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

        private static unsafe void AddTypedAttributes(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "typed");
            var attributeSpaceId = H5S.create_simple(3, new ulong[] { 2, 2, 3 }, new ulong[] { 3, 3, 4 });

            // numeric attributes
            var attributeId1 = H5A.create(groupId, "A1", H5T.NATIVE_UINT8, attributeSpaceId);
            var attributeData1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData1)
            {
                res = H5A.write(attributeId1, H5T.NATIVE_UINT8, new IntPtr(ptr));
            }

            res = H5A.close(attributeId1);

            var attributeData2 = new ushort[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            var attributeId2 = H5A.create(groupId, "A2", H5T.NATIVE_UINT16, attributeSpaceId);

            fixed (void* ptr = attributeData2)
            {
                res = H5A.write(attributeId2, H5T.NATIVE_UINT16, new IntPtr(ptr));
            }

            res = H5A.close(attributeId2);

            var attributeId3 = H5A.create(groupId, "A3", H5T.NATIVE_UINT32, attributeSpaceId);
            var attributeData3 = new uint[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData3)
            {
                res = H5A.write(attributeId3, H5T.NATIVE_UINT32, new IntPtr(ptr));
            }

            res = H5A.close(attributeId3);

            var attributeId4 = H5A.create(groupId, "A4", H5T.NATIVE_UINT64, attributeSpaceId);
            var attributeData4 = new ulong[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData4)
            {
                res = H5A.write(attributeId4, H5T.NATIVE_UINT64, new IntPtr(ptr));
            }

            res = H5A.close(attributeId4);

            var attributeId5 = H5A.create(groupId, "A5", H5T.NATIVE_INT8, attributeSpaceId);
            var attributeData5 = new sbyte[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData5)
            {
                res = H5A.write(attributeId5, H5T.NATIVE_INT8, new IntPtr(ptr));
            }

            res = H5A.close(attributeId5);

            var attributeId6 = H5A.create(groupId, "A6", H5T.NATIVE_INT16, attributeSpaceId);
            var attributeData6 = new short[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData6)
            {
                res = H5A.write(attributeId6, H5T.NATIVE_INT16, new IntPtr(ptr));
            }

            res = H5A.close(attributeId6);

            var attributeId7 = H5A.create(groupId, "A7", H5T.NATIVE_INT32, attributeSpaceId);
            var attributeData7 = new int[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData7)
            {
                res = H5A.write(attributeId7, H5T.NATIVE_INT32, new IntPtr(ptr));
            }

            res = H5A.close(attributeId7);

            var attributeId8 = H5A.create(groupId, "A8", H5T.NATIVE_INT64, attributeSpaceId);
            var attributeData8 = new long[] { 0, 1, 2, 3, 4, 5, 6, -7, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData8)
            {
                res = H5A.write(attributeId8, H5T.NATIVE_INT64, new IntPtr(ptr));
            }

            res = H5A.close(attributeId8);

            var attributeId9 = H5A.create(groupId, "A9", H5T.NATIVE_FLOAT, attributeSpaceId);
            var attributeData9 = new float[] { 0, 1, 2, 3, 4, 5, 6, (float)-7.99, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData9)
            {
                res = H5A.write(attributeId9, H5T.NATIVE_FLOAT, new IntPtr(ptr));
            }

            res = H5A.close(attributeId9);

            var attributeId10 = H5A.create(groupId, "A10", H5T.NATIVE_DOUBLE, attributeSpaceId);
            var attributeData10 = new double[] { 0, 1, 2, 3, 4, 5, 6, -7.99, 8, 9, 10, 11 };

            fixed (void* ptr = attributeData10)
            {
                res = H5A.write(attributeId10, H5T.NATIVE_DOUBLE, new IntPtr(ptr));
            }

            res = H5A.close(attributeId10);

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
            var attributeData14 = TestUtils.NonNullableTestStructData;

            fixed (void* ptr = attributeData14)
            {
                res = H5A.write(attributeId14, attributeTypeId14, new IntPtr(ptr));
            }

            res = H5T.close(attributeTypeId14);
            res = H5A.close(attributeId14);

            // nullable struct
            var attributeTypeId15 = TestUtils.GetHdfTypeIdFromType(typeof(TestStructString));
            var attributeId15 = H5A.create(groupId, "A15", attributeTypeId15, attributeSpaceId);
            var attributeData15 = TestUtils.StringTestStructData;

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

        private static unsafe void AddMassAttributes(long fileId)
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

                var attributeData = TestUtils.NonNullableTestStructData;

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

        private static unsafe void AddHugeAttribute(long fileId, H5F.libver_t version)
        {
            long res;

            var length = version switch
            {
                H5F.libver_t.EARLIEST => 16368UL, // max 64 kb in object header
                _ => (ulong)TestUtils.HugeData.Length,
            };

            var groupId = H5G.create(fileId, "large");
            var attributeSpaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var attributeId = H5A.create(groupId, "large", H5T.NATIVE_INT32, attributeSpaceId);
            var attributeData = TestUtils.HugeData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.NATIVE_INT32, new IntPtr(ptr));
            }

            res = H5S.close(attributeSpaceId);
            res = H5A.close(attributeId);
            res = H5G.close(groupId);
        }

        private static unsafe void AddTinyAttribute(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "tiny");
            var attributeSpaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var attributeId = H5A.create(groupId, "tiny", H5T.NATIVE_UINT8, attributeSpaceId);
            var attributeData = TestUtils.TinyData;

            fixed (void* ptr = attributeData)
            {
                res = H5A.write(attributeId, H5T.NATIVE_UINT8, new IntPtr(ptr));
            }

            res = H5S.close(attributeSpaceId);
            res = H5A.close(attributeId);
            res = H5G.close(groupId);
        }

        private static void AddLinks(long fileId)
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

        private static unsafe void AddCompactDataset(long fileId)
        {
            long res;

            var groupId = H5G.create(fileId, "compact");
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_layout(dcpl_id, H5D.layout_t.COMPACT);
            var datasetId = H5D.create(groupId, "compact", H5T.NATIVE_UINT8, datasetSpaceId, dcpl_id: dcpl_id);
            var datasetData = TestUtils.TinyData;

            fixed (void* ptr = datasetData)
            {
                res = H5D.write(datasetId, H5T.NATIVE_UINT8, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        private static unsafe void AddContiguousDataset(long fileId)
        {
            long res;

            var length = (ulong)TestUtils.HugeData.Length;
            var groupId = H5G.create(fileId, "contiguous");         
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var datasetId = H5D.create(groupId, "contiguous", H5T.NATIVE_INT, datasetSpaceId);
            var datasetData = TestUtils.HugeData;

            fixed (void* ptr = datasetData)
            {
                res = H5D.write(datasetId, H5T.NATIVE_INT, datasetSpaceId, H5S.ALL, 0, new IntPtr(ptr));
            }

            res = H5D.close(datasetId);
            res = H5G.close(groupId);
        }

        private static unsafe void AddChunkedDataset(long fileId)
        {
            long res;

            var length = (ulong)TestUtils.HugeData.Length;
            var groupId = H5G.create(fileId, "chunked");
            var datasetSpaceId = H5S.create_simple(1, new ulong[] { length }, new ulong[] { length });
            var dcpl_id = H5P.create(H5P.DATASET_CREATE);
            res = H5P.set_chunk(dcpl_id, 1, new ulong[] { 1000 });
            var datasetId = H5D.create(groupId, "chunked", H5T.NATIVE_INT, datasetSpaceId, dcpl_id: dcpl_id);
            var datasetData = TestUtils.HugeData;

            fixed (void* ptr = datasetData)
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

                    H5T.insert(typeId, fieldInfo.Name, Marshal.OffsetOf(elementType, fieldInfo.Name), fieldType);

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