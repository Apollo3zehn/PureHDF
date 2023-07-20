using System.Reflection;

namespace PureHDF;

/// <summary>
/// An HDF5 attribute.
/// </summary>
public interface IH5Attribute
{
    /// <summary>
    /// Gets the attribute name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the data space.
    /// </summary>
    IH5Dataspace Space { get; }

    /// <summary>
    /// Gets the data type.
    /// </summary>
    IH5DataType Type { get; }

    /// <summary>
    /// Reads the data. The type parameter <typeparamref name="T"/> must match the <see langword="unmanaged" /> constraint.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    T[] Read<T>() where T : unmanaged;

    /// <summary>
    /// Reads the compound data. The type parameter <typeparamref name="T"/> must match the <see langword="struct" /> constraint. Nested fields with nullable references are not supported.
    /// </summary>
    /// <typeparam name="T">The type of the data to read.</typeparam>
    /// <param name="getName">An optional function to map the field names of <typeparamref name="T"/> to the member names of the HDF5 compound type.</param>
    /// <returns>The read data as array of <typeparamref name="T"/>.</returns>
    T[] ReadCompound<T>(Func<FieldInfo, string?>? getName = null) where T : struct;

    /// <summary>
    /// Reads the compound data. This is the slowest but most flexible option to read compound data as no prior type knowledge is required.
    /// </summary>
    /// <returns>The read data as array of a dictionary with the keys corresponding to the compound member names and the values being the member data.</returns>
    Dictionary<string, object?>[] ReadCompound();

    /// <summary>
    /// Reads the string data.
    /// </summary>
    /// <returns>The read data as array of <see cref="string"/>.</returns>
    string[] ReadString();

    /// <summary>
    /// Reads the variable-length sequence data.
    /// </summary>
    /// <param name="fileSelection">The selection within the source HDF5 dataset.</param>
    /// <param name="memorySelection">The selection within the destination memory.</param>
    /// <param name="memoryDims">The dimensions of the destination memory buffer.</param>
    /// <returns>The read data as jagged array of <typeparamref name="T"/>.</returns>
    T[]?[] ReadVariableLength<T>(Selection? fileSelection = null, Selection? memorySelection = null, ulong[]? memoryDims = null) where T : struct;
}