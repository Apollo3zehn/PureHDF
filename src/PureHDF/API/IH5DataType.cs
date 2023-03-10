namespace PureHDF;

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
    FixedPointType FixedPoint { get; }

    /// <summary>
    /// Gets the floating-point data type.
    /// </summary>
    FloatingPointType FloatingPoint { get; }

    /// <summary>
    /// Gets the string data type.
    /// </summary>
    StringType String { get; }

    /// <summary>
    /// Gets the bitfield data type.
    /// </summary>
    BitFieldType BitField { get; }

    /// <summary>
    /// Gets the opaque data type.
    /// </summary>
    OpaqueType Opaque { get; }

    /// <summary>
    /// Gets the compound data type.
    /// </summary>
    CompoundType Compound { get; }

    /// <summary>
    /// Gets the reference data type.
    /// </summary>
    ReferenceType Reference { get; }

    /// <summary>
    /// Gets the enumeration data type.
    /// </summary>
    EnumerationType Enumeration { get; }

    /// <summary>
    /// Gets the variable-length data type.
    /// </summary>
    VariableLengthType VariableLength { get; }

    /// <summary>
    /// Gets the array data type.
    /// </summary>
    ArrayType Array { get; }

    #region Types

    /// <summary>
    /// The fixed-point data type.
    /// </summary>
    public class FixedPointType
    {
        private readonly FixedPointBitFieldDescription _bitField;

        internal FixedPointType(
            FixedPointBitFieldDescription bitField)
        {
            _bitField = bitField;

            IsSigned = _bitField.IsSigned;
        }

        /// <summary>
        /// Gets a boolean which indicates if the data type is signed.
        /// </summary>
        public bool IsSigned { get; }
    }

    /// <summary>
    /// The floating-point data type.
    /// </summary>
    public class FloatingPointType
    {
        //
    }

    /// <summary>
    /// The string data type.
    /// </summary>
    public class StringType
    {
        //
    }

    /// <summary>
    /// The bitfield data type.
    /// </summary>
    public class BitFieldType
    {
        //
    }

    /// <summary>
    /// The opaque data type.
    /// </summary>
    public class OpaqueType
    {
        internal OpaqueType(
            OpaquePropertyDescription property)
        {
            Tag = property.Tag;
        }

        /// <summary>
        /// Gets a description for the opaque type.
        /// </summary>
        public string Tag { get; }
    }

    /// <summary>
    /// The compound data type.
    /// </summary>
    public class CompoundType
    {
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

        private readonly CompoundPropertyDescription[] _properties;

        internal CompoundType(
            CompoundPropertyDescription[] properties)
        {
            _properties = properties;

            Members = _properties
                .Select(property => new CompoundMember(
                    property.Name,
                    (int)property.MemberByteOffset,
                    new H5DataType(property.MemberTypeMessage)))
                .ToArray();
        }

        /// <summary>
        /// Gets an array of members.
        /// </summary>
        public CompoundMember[] Members { get; }
    }

    /// <summary>
    /// The reference data type.
    /// </summary>
    public class ReferenceType
    {
        //
    }

    /// <summary>
    /// The enumeration data type.
    /// </summary>
    public class EnumerationType
    {
        //
    }

    /// <summary>
    /// The variable-length data type.
    /// </summary>
    public class VariableLengthType
    {
        //
    }

    /// <summary>
    /// The array data type.
    /// </summary>
    public class ArrayType
    {
        private readonly ArrayPropertyDescription _property;

        internal ArrayType(
            ArrayPropertyDescription property)
        {
            _property = property;

            BaseType = new H5DataType(_property.BaseType);
        }

        /// <summary>
        /// Gets the base data type.
        /// </summary>
        public IH5DataType BaseType { get; }
    }

    #endregion
}