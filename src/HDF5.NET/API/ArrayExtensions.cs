namespace HDF5.NET
{
    public static partial class ArrayExtensions
    {
        public static unsafe T[,] ToArray2D<T>(this T[] data, long dim0, long dim1)
            where T : unmanaged
        {
            var dims = new long[] { dim0, dim1 };
            ArrayExtensions.ValidateInputData(data, dims);
            var output = new T[dims[0], dims[1]];

            fixed (void* ptr = output)
            {
                ArrayExtensions.CopyData(data, ptr);
            }

            return output;
        }

        public static unsafe T[,,] ToArray3D<T>(this T[] data, long dim0, long dim1, long dim2) 
            where T : unmanaged
        {
            var dims = new long[] { dim0, dim1, dim2 };
            ArrayExtensions.ValidateInputData(data, dims);
            var output = new T[dims[0], dims[1], dims[2]];

            fixed (void* ptr = output)
            {
                ArrayExtensions.CopyData(data, ptr);
            }

            return output;
        }

        public static unsafe T[,,,] ToArray4D<T>(this T[] data, long dim0, long dim1, long dim2, long dim3) 
            where T : unmanaged
        {
            var dims = new long[] { dim0, dim1, dim2, dim3 };
            ArrayExtensions.ValidateInputData(data, dims);
            var output = new T[dims[0], dims[1], dims[2], dims[3]];

            fixed (void* ptr = output)
            {
                ArrayExtensions.CopyData(data, ptr);
            }

            return output;
        }

        public static unsafe T[,,,,] ToArray5D<T>(this T[] data, long dim0, long dim1, long dim2, long dim3, long dim4) 
            where T : unmanaged
        {
            var dims = new long[] { dim0, dim1, dim2, dim3, dim4 };
            ArrayExtensions.ValidateInputData(data, dims);
            var output = new T[dims[0], dims[1], dims[2], dims[3], dims[4]];

            fixed (void* ptr = output)
            {
                ArrayExtensions.CopyData(data, ptr);
            }

            return output;
        }

        public static unsafe T[,,,,,] ToArray6D<T>(this T[] data, long dim0, long dim1, long dim2, long dim3, long dim4, long dim5) 
            where T : unmanaged
        {
            var dims = new long[] { dim0, dim1, dim2, dim3, dim4, dim5 };
            ArrayExtensions.ValidateInputData(data, dims);
            var output = new T[dims[0], dims[1], dims[2], dims[3], dims[4], dims[5]];

            fixed (void* ptr = output)
            {
                ArrayExtensions.CopyData(data, ptr);
            }

            return output;
        }
    }
}
