using System.Reflection;
using PureHDF.VOL.Native;

namespace PureHDF.VOL.Hsds
{
    internal class H5Dataset : IH5Dataset
    {
        public IH5File File => throw new NotImplementedException();

        public IH5Dataspace Space => throw new NotImplementedException();

        public IH5DataType Type => throw new NotImplementedException();

        public IH5DataLayout Layout => throw new NotImplementedException();

        public IH5FillValue FillValue => throw new NotImplementedException();

        public IEnumerable<IH5Attribute> Attributes => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IQueryable<T> AsQueryable<T>(Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public IH5Attribute Attribute(string name)
        {
            throw new NotImplementedException();
        }

        public bool AttributeExists(string name)
        {
            throw new NotImplementedException();
        }

        public byte[] Read(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default)
        {
            throw new NotImplementedException();
        }

        public T[] Read<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public void Read<T>(Memory<T> buffer, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> ReadAsync(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default)
        {
            throw new NotImplementedException();
        }

        public Task<T[]> ReadAsync<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public Task ReadAsync<T>(Memory<T> buffer, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            throw new NotImplementedException();
        }

        public T[] ReadCompound<T>(Func<FieldInfo, string>? getName = null, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default) where T : struct
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, object?>[] ReadCompound(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default)
        {
            throw new NotImplementedException();
        }

        public Task<T[]> ReadCompoundAsync<T>(Func<FieldInfo, string>? getName = null, Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default) where T : struct
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, object?>[]> ReadCompoundAsync(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default)
        {
            throw new NotImplementedException();
        }

        public string?[] ReadString(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default)
        {
            throw new NotImplementedException();
        }

        public Task<string?[]> ReadStringAsync(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null, H5DatasetAccess datasetAccess = default)
        {
            throw new NotImplementedException();
        }
    }
}