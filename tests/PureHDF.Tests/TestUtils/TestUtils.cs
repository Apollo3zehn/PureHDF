using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using HDF.PInvoke;
using Xunit.Abstractions;

namespace PureHDF.Tests;

public partial class TestUtils
{
    public static string? DumpH5File(string filePath)
    {
        var dump = default(string);

        var h5dumpProcess = new Process 
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "h5dump",
                Arguments = filePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        h5dumpProcess.Start();

        while (!h5dumpProcess.StandardOutput.EndOfStream)
        {
            var line = h5dumpProcess.StandardOutput.ReadLine();

            if (dump is null)
                dump = line;

            else
                dump += Environment.NewLine + line;
        }

        while (!h5dumpProcess.StandardError.EndOfStream)
        {
            var line = h5dumpProcess.StandardError.ReadLine();

            if (dump is null)
                dump = line;

            else
                dump += Environment.NewLine + line;
        }

        return dump;
    }

    public static void RunForAllVersions(Action<H5F.libver_t> action)
    {
        var versions = new H5F.libver_t[]
        {
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
        _ = H5F.close(fileId);

        return filePath;
    }

    public static unsafe void Add<T>(ContainerType container, long fileId, string groupName, string elementName, long typeId, Span<T> data, long cpl = 0, long apl = 0)
        where T : unmanaged
    {
        var length = (ulong)data.Length;
        Add(container, fileId, groupName, elementName, typeId, data, length, cpl, apl);
    }

    public static unsafe void Add<T>(ContainerType container, long fileId, string groupName, string elementName, long typeId, Span<T> data, ulong length, long cpl = 0, long apl = 0)
        where T : unmanaged
    {
        var dims0 = new ulong[] { length };
        Add(container, fileId, groupName, elementName, typeId, data, dims0, dims0, cpl, apl);
    }

    public static unsafe void Add<T>(ContainerType container, long fileId, string groupName, string elementName, long typeId, Span<T> data, ulong[] dims0, ulong[]? dims1 = default, long cpl = 0, long apl = 0)
        where T : unmanaged
    {
        fixed (void* dataPtr = data)
        {
            Add(container, fileId, groupName, elementName, typeId, dataPtr, dims0, dims1, cpl, apl);
        }
    }

    public static unsafe void Add(ContainerType container, long fileId, string groupName, string elementName, long typeId, void* dataPtr, ulong length, long cpl = 0, long apl = 0)
    {
        var dims = new ulong[] { length };
        Add(container, fileId, groupName, elementName, typeId, dataPtr, dims, dims, cpl, apl);
    }

    public static unsafe void Add(ContainerType container, long fileId, string groupName, string elementName, long typeId, void* dataPtr, ulong[] dims, ulong[]? maxDims = default, long cpl = 0, long apl = 0)
    {
        maxDims ??= dims;

        var spaceId = H5S.create_simple(dims.Length, dims, maxDims);
        Add(container, fileId, groupName, elementName, typeId, dataPtr, spaceId, cpl, apl);
        _ = H5S.close(spaceId);
    }

    public static unsafe void Add(ContainerType container, long fileId, string groupName, string elementName, long typeId, void* dataPtr, long spaceId, long cpl = 0, long apl = 0)
    {
        long groupId;

        if (H5L.exists(fileId, groupName) > 0)
            groupId = H5G.open(fileId, groupName);
        else
            groupId = H5G.create(fileId, groupName);

        long id;

        if (container == ContainerType.Dataset)
        {
            id = H5D.create(groupId, Encoding.UTF8.GetBytes(elementName), typeId, spaceId, dcpl_id: cpl, dapl_id: apl);

            if (id == -1)
                throw new Exception("Could not create dataset.");

            if ((int)dataPtr != 0)
                _ = H5D.write(id, typeId, spaceId, H5S.ALL, 0, new IntPtr(dataPtr));

            _ = H5D.close(id);
        }
        else
        {
            id = H5A.create(groupId, Encoding.UTF8.GetBytes(elementName), typeId, spaceId, acpl_id: cpl);

            if (id == -1)
                throw new Exception("Could not create attribute.");

            if ((int)dataPtr != 0)
                _ = H5A.write(id, typeId, new IntPtr(dataPtr));

            _ = H5A.close(id);
        }

        _ = H5G.close(groupId);
    }

    public static bool ReadAndCompare<T>(IH5Dataset dataset, T[] expected)
        where T : unmanaged
    {
        var actual = dataset.Read<T[]>();
        return actual.SequenceEqual(expected);
    }

    public static void CaptureHdfLibOutput(ITestOutputHelper logger)
    {
        _ = H5E.set_auto(H5E.DEFAULT, ErrorDelegateMethod, IntPtr.Zero);

        int ErrorDelegateMethod(long estack, IntPtr client_data)
        {
            _ = H5E.walk(estack, H5E.direction_t.H5E_WALK_DOWNWARD, WalkDelegateMethod, IntPtr.Zero);
            return 0;
        }

        int WalkDelegateMethod(uint n, ref H5E.error_t err_desc, IntPtr client_data)
        {
            logger.WriteLine($"{n}: {err_desc.desc}");
            return 0;
        }
    }

    private static long GetHdfTypeIdFromType(Type type, ulong? arrayLength = default, bool includeH5NameAttribute = false)
    {
        if (type == typeof(bool))
            return H5T.NATIVE_UINT8;

        else if (type == typeof(byte))
            return H5T.NATIVE_UINT8;

        else if (type == typeof(sbyte))
            return H5T.NATIVE_INT8;

        else if (type == typeof(ushort))
            return H5T.NATIVE_UINT16;

        else if (type == typeof(short))
            return H5T.NATIVE_INT16;

        else if (type == typeof(uint))
            return H5T.NATIVE_UINT32;

        else if (type == typeof(int))
            return H5T.NATIVE_INT32;

        else if (type == typeof(ulong))
            return H5T.NATIVE_UINT64;

        else if (type == typeof(long))
            return H5T.NATIVE_INT64;

#if NET7_0_OR_GREATER
        else if (type == typeof(UInt128))
        {
            var typeId = H5T.copy(H5T.NATIVE_UINT64);
            _ = H5T.set_size(typeId, 16);
            _ = H5T.set_precision(typeId, 128);

            return typeId;
        }

        else if (type == typeof(Int128))
        {
            var typeId = H5T.copy(H5T.NATIVE_INT64);
            _ = H5T.set_size(typeId, 16);
            _ = H5T.set_precision(typeId, 128);
            
            return typeId;
        }
#endif

#if NET5_0_OR_GREATER
        else if (type == typeof(Half))
        {
            var typeId = H5T.copy(H5T.NATIVE_FLOAT);

            _ = H5T.set_fields(typeId,
                spos: (nint)15,
                epos: (nint)10,
                esize: (nint)5,
                mpos: (nint)0,
                msize: (nint)10);

            _ = H5T.set_ebias(typeId, (nint)15);
            _ = H5T.set_precision(typeId, (nint)16);
            _ = H5T.set_size(typeId, (nint)2);                

            return typeId;
        }
#endif

        else if (type == typeof(float))
            return H5T.NATIVE_FLOAT;

        else if (type == typeof(double))
            return H5T.NATIVE_DOUBLE;

        // issues: https://en.wikipedia.org/wiki/Long_double
        //else if (elementType == typeof(decimal))
        //    return H5T.NATIVE_LDOUBLE;

        else if (type.IsArray && arrayLength.HasValue)
        {
            var elementType = type.GetElementType()!;
            var dims = new ulong[] { arrayLength.Value };
            var typeId = H5T.array_create(GetHdfTypeIdFromType(elementType), rank: 1, dims);

            return typeId;
        }

        else if (type.IsEnum)
        {
            var baseTypeId = GetHdfTypeIdFromType(Enum.GetUnderlyingType(type));
            var typeId = H5T.enum_create(baseTypeId);

            foreach (var value in Enum.GetValues(type))
            {
                var value_converted = Convert.ToInt64(value);
                var name = Enum.GetName(type, value_converted);

                var handle = GCHandle.Alloc(value_converted, GCHandleType.Pinned);
                _ = H5T.enum_insert(typeId, name, handle.AddrOfPinnedObject());
            }

            return typeId;
        }

        else if (type == typeof(string) || type == typeof(IntPtr))
        {
            var typeId = H5T.copy(H5T.C_S1);

            _ = H5T.set_size(typeId, H5T.VARIABLE);
            _ = H5T.set_cset(typeId, H5T.cset_t.UTF8);

            return typeId;
        }
        else if (type.IsValueType && !type.IsPrimitive)
        {
            var typeId = H5T.create(H5T.class_t.COMPOUND, new IntPtr(Marshal.SizeOf(type)));

            foreach (var fieldInfo in type.GetFields())
            {
                var marshalAsAttribute = fieldInfo.GetCustomAttribute<MarshalAsAttribute>();

                var arraySize = marshalAsAttribute is not null && marshalAsAttribute.Value == UnmanagedType.ByValArray
                    ? new ulong?((ulong)marshalAsAttribute.SizeConst)
                    : null;

                var fieldType = GetHdfTypeIdFromType(fieldInfo.FieldType, arraySize);

                var nameAttribute = includeH5NameAttribute
                    ? fieldInfo.GetCustomAttribute<H5NameAttribute>(true)
                    : default;

                var hdfFieldName = nameAttribute is not null ? nameAttribute.Name : fieldInfo.Name;

                _ = H5T.insert(typeId, hdfFieldName, Marshal.OffsetOf(type, fieldInfo.Name), fieldType);

                if (H5I.is_valid(fieldType) > 0)
                    _ = H5T.close(fieldType);
            }

            return typeId;
        }
        else
        {
            throw new NotSupportedException();
        }
    }
}