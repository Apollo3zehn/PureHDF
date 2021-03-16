using System;
using System.Reflection;

namespace HDF5.NET
{
    public partial class H5Dataset : H5AttributableObject
    {
        #region Properties

        public H5File File { get; }

        #endregion

        #region Public

        public byte[] Read(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default)
        {
            var result = this.Read<byte>(
                null,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        public T[] Read<T>(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            var result = this.Read<T>(
                null,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (result is null)
                throw new Exception("The buffer is null. This should never happen.");

            return result;
        }

        public void Read<T>(
            Memory<T> buffer,
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default) where T : unmanaged
        {
            this.Read(
                buffer,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);
        }

        public T[] ReadCompound<T>(
           Func<FieldInfo, string>? getName = default,
           Selection? fileSelection = default,
           Selection? memorySelection = default,
           ulong[]? memoryDims = default,
           H5DatasetAccess datasetAccess = default) where T : struct
        {
            var data = this.Read<byte>(
                null,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false);

            if (data is null)
                throw new Exception("The buffer is null. This should never happen.");

            if (getName is null)
                getName = fieldInfo => fieldInfo.Name;

            return H5Utils.ReadCompound<T>(this.Datatype, this.Dataspace, this.Context.Superblock, data, getName);
        }

        public string[] ReadString(
            Selection? fileSelection = default,
            Selection? memorySelection = default,
            ulong[]? memoryDims = default,
            H5DatasetAccess datasetAccess = default)
        {
            var data = this.Read<byte>(
                null,
                fileSelection,
                memorySelection,
                memoryDims,
                datasetAccess,
                skipShuffle: false,
                skipTypeCheck: true);

            if (data is null)
                throw new Exception("The buffer is null. This should never happen.");

            return H5Utils.ReadString(this.Datatype, data, this.Context.Superblock);
        }

        #endregion
    }
}
