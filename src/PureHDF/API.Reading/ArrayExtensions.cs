namespace PureHDF;

/// <summary>
/// Contains extensions to simplify the conversion of a 1-dimensional array to an up 6-simensional array.
/// </summary>
public static partial class ArrayExtensions
{
    /// <summary>
    /// Converts the 1-dimensional input array into a 2-dimensional output array by coping the data. See also <seealso href="https://github.com/Apollo3zehn/PureHDF#722-high-performance-method-2d-only">PureHDF</seealso> for a copy-free version.
    /// </summary>
    /// <typeparam name="T">The base type of the array to convert.</typeparam>
    /// <param name="data">The array to convert.</param>
    /// <param name="dim0">The size of dimension 0.</param>
    /// <param name="dim1">The size of dimension 1.</param>
    /// <returns>A 2-dimensional array.</returns>
    public static unsafe T[,] ToArray2D<T>(this T[] data, long dim0, long dim1)
        where T : unmanaged
    {
        var dims = new long[] { dim0, dim1 };
        ValidateInputData(data, dims);
        var output = new T[dims[0], dims[1]];

#if NET6_0_OR_GREATER
        CopyData(data.AsSpan(), output);
#else
        fixed (void* ptr = output)
        {
            CopyData(data.AsSpan(), ptr);
        }
#endif

        return output;
    }

    /// <summary>
    /// Converts the 1-dimensional input array into a 3-dimensional output array by coping the data.
    /// </summary>
    /// <typeparam name="T">The base type of the array to convert.</typeparam>
    /// <param name="data">The array to convert.</param>
    /// <param name="dim0">The size of dimension 0.</param>
    /// <param name="dim1">The size of dimension 1.</param>
    /// <param name="dim2">The size of dimension 2.</param>
    /// <returns>A 3-dimensional array.</returns>
    public static unsafe T[,,] ToArray3D<T>(this T[] data, long dim0, long dim1, long dim2)
        where T : unmanaged
    {
        var dims = new long[] { dim0, dim1, dim2 };
        ValidateInputData(data, dims);
        var output = new T[dims[0], dims[1], dims[2]];

#if NET6_0_OR_GREATER
        CopyData(data.AsSpan(), output);
#else
        fixed (void* ptr = output)
        {
            CopyData(data.AsSpan(), ptr);
        }
#endif

        return output;
    }

    /// <summary>
    /// Converts the 1-dimensional input array into a 4-dimensional output array by coping the data.
    /// </summary>
    /// <typeparam name="T">The base type of the array to convert.</typeparam>
    /// <param name="data">The array to convert.</param>
    /// <param name="dim0">The size of dimension 0.</param>
    /// <param name="dim1">The size of dimension 1.</param>
    /// <param name="dim2">The size of dimension 2.</param>
    /// <param name="dim3">The size of dimension 3.</param>
    /// <returns>A 4-dimensional array.</returns>
    public static unsafe T[,,,] ToArray4D<T>(this T[] data, long dim0, long dim1, long dim2, long dim3)
        where T : unmanaged
    {
        var dims = new long[] { dim0, dim1, dim2, dim3 };
        ValidateInputData(data, dims);
        var output = new T[dims[0], dims[1], dims[2], dims[3]];

#if NET6_0_OR_GREATER
        CopyData(data.AsSpan(), output);
#else
        fixed (void* ptr = output)
        {
            CopyData(data.AsSpan(), ptr);
        }
#endif

        return output;
    }

    /// <summary>
    /// Converts the 1-dimensional input array into a 5-dimensional output array by coping the data.
    /// </summary>
    /// <typeparam name="T">The base type of the array to convert.</typeparam>
    /// <param name="data">The array to convert.</param>
    /// <param name="dim0">The size of dimension 0.</param>
    /// <param name="dim1">The size of dimension 1.</param>
    /// <param name="dim2">The size of dimension 2.</param>
    /// <param name="dim3">The size of dimension 3.</param>
    /// <param name="dim4">The size of dimension 4.</param>
    /// <returns>A 5-dimensional array.</returns>
    public static unsafe T[,,,,] ToArray5D<T>(this T[] data, long dim0, long dim1, long dim2, long dim3, long dim4)
        where T : unmanaged
    {
        var dims = new long[] { dim0, dim1, dim2, dim3, dim4 };
        ValidateInputData(data, dims);
        var output = new T[dims[0], dims[1], dims[2], dims[3], dims[4]];

#if NET6_0_OR_GREATER
        CopyData(data.AsSpan(), output);
#else
        fixed (void* ptr = output)
        {
            CopyData(data.AsSpan(), ptr);
        }
#endif

        return output;
    }

    /// <summary>
    /// Converts the 1-dimensional input array into a 6-dimensional output array by coping the data.
    /// </summary>
    /// <typeparam name="T">The base type of the array to convert.</typeparam>
    /// <param name="data">The array to convert.</param>
    /// <param name="dim0">The size of dimension 0.</param>
    /// <param name="dim1">The size of dimension 1.</param>
    /// <param name="dim2">The size of dimension 2.</param>
    /// <param name="dim3">The size of dimension 3.</param>
    /// <param name="dim4">The size of dimension 4.</param>
    /// <param name="dim5">The size of dimension 5.</param>
    /// <returns>A 6-dimensional array.</returns>
    public static unsafe T[,,,,,] ToArray6D<T>(this T[] data, long dim0, long dim1, long dim2, long dim3, long dim4, long dim5)
        where T : unmanaged
    {
        var dims = new long[] { dim0, dim1, dim2, dim3, dim4, dim5 };
        ValidateInputData(data, dims);
        var output = new T[dims[0], dims[1], dims[2], dims[3], dims[4], dims[5]];

#if NET6_0_OR_GREATER
        CopyData(data.AsSpan(), output);
#else
        fixed (void* ptr = output)
        {
            CopyData(data.AsSpan(), ptr);
        }
#endif

        return output;
    }
}