namespace PureHDF;

/// <summary>
/// The fixed-point data type.
/// </summary>
public interface IFixedPointType
{
    /// <summary>
    /// Gets a boolean which indicates if the data type is signed.
    /// </summary>
    bool IsSigned { get; }
}

/// <summary>
/// The floating-point data type.
/// </summary>
public interface IFloatingPointType
{
    //
}

/// <summary>
/// The string data type.
/// </summary>
public interface IStringType
{
    //
}

/// <summary>
/// The bitfield data type.
/// </summary>
public interface IBitFieldType
{
    //
}

/// <summary>
/// The opaque data type.
/// </summary>
public interface IOpaqueType
{
    /// <summary>
    /// Gets a description for the opaque type.
    /// </summary>
    string Tag { get; }
}

/// <summary>
/// A compound member.
/// </summary>
/// <param name="Name">The member name.</param>
/// <param name="Offset">The offset of the member in the parent compound data type.</param>
/// <param name="Type">The member data type.</param>
public record CompoundMember(
    string Name,
    int Offset,
    IH5DataType Type
);

/// <summary>
/// The compound data type.
/// </summary>
public interface ICompoundType
{
    /// <summary>
    /// Gets an array of members.
    /// </summary>
    CompoundMember[] Members { get; }
}

/// <summary>
/// The reference data type.
/// </summary>
public interface IReferenceType
{
    //
}

/// <summary>
/// The enumeration data type.
/// </summary>
public interface IEnumerationType
{
    //
}

/// <summary>
/// The variable-length data type.
/// </summary>
public interface IVariableLengthType
{
    //
}

/// <summary>
/// The array data type.
/// </summary>
public interface IArrayType
{
    /// <summary>
    /// Gets the base data type.
    /// </summary>
    IH5DataType BaseType { get; }
}

/// <summary>
/// An HDF5 data type.
/// </summary>
public interface IH5DataType
{
    /// <summary>
    /// Gets the data type class.
    /// </summary>
    H5DataTypeClass Class { get; }

    /// <summary>
    /// Gets the size of the data type in bytes.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets the fixed-point data type.
    /// </summary>        
    IFixedPointType FixedPoint { get; }

    /// <summary>
    /// Gets the floating-point data type.
    /// </summary>
    IFloatingPointType FloatingPoint { get; }

    /// <summary>
    /// Gets the string data type.
    /// </summary>
    IStringType String { get; }

    /// <summary>
    /// Gets the bitfield data type.
    /// </summary>
    IBitFieldType BitField { get; }

    /// <summary>
    /// Gets the opaque data type.
    /// </summary>
    IOpaqueType Opaque { get; }

    /// <summary>
    /// Gets the compound data type.
    /// </summary>
    ICompoundType Compound { get; }

    /// <summary>
    /// Gets the reference data type.
    /// </summary>
    IReferenceType Reference { get; }

    /// <summary>
    /// Gets the enumeration data type.
    /// </summary>
    IEnumerationType Enumeration { get; }

    /// <summary>
    /// Gets the variable-length data type.
    /// </summary>
    IVariableLengthType VariableLength { get; }

    /// <summary>
    /// Gets the array data type.
    /// </summary>
    IArrayType Array { get; }
}