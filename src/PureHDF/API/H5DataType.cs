namespace PureHDF
{
    /// <summary>
    /// An HDF5 data type.
    /// </summary>
    public partial class H5DataType
    {
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
            //
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
                H5DataType Type
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
            public H5DataType BaseType { get; }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the data type class.
        /// </summary>
        public H5DataTypeClass Class => (H5DataTypeClass)_dataType.Class;

        /// <summary>
        /// Gets the size of the data type in bytes.
        /// </summary>
        public int Size => (int)_dataType.Size;

        /// <summary>
        /// Gets the fixed-point data type.
        /// </summary>
        public FixedPointType FixedPoint
        {
            get
            {
                if (_fixedPoint is null)
                {
                    if (Class == H5DataTypeClass.FixedPoint)
                        _fixedPoint = new FixedPointType(
                            (FixedPointBitFieldDescription)_dataType.BitField);

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.FixedPoint}.");
                }

                return _fixedPoint;
            }
        }

        /// <summary>
        /// Gets the floating-point data type.
        /// </summary>
        public FloatingPointType FloatingPoint
        {
            get
            {
                if (_floatingPoint is null)
                {
                    if (Class == H5DataTypeClass.FloatingPoint)
                        _floatingPoint = new FloatingPointType();

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.FloatingPoint}.");
                }

                return _floatingPoint;
            }
        }

        /// <summary>
        /// Gets the string data type.
        /// </summary>
        public StringType String
        {
            get
            {
                if (_string is null)
                {
                    if (Class == H5DataTypeClass.String)
                        _string = new StringType();

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.String}.");
                }

                return _string;
            }
        }

        /// <summary>
        /// Gets the bitfield data type.
        /// </summary>
        public BitFieldType BitField
        {
            get
            {
                if (_bitField is null)
                {
                    if (Class == H5DataTypeClass.BitField)
                        _bitField = new BitFieldType();

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.BitField}.");
                }

                return _bitField;
            }
        }

        /// <summary>
        /// Gets the opaque data type.
        /// </summary>
        public OpaqueType Opaque
        {
            get
            {
                if (_opaque is null)
                {
                    if (Class == H5DataTypeClass.Opaque)
                        _opaque = new OpaqueType();

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.Opaque}.");
                }

                return _opaque;
            }
        }

        /// <summary>
        /// Gets the compound data type.
        /// </summary>
        public CompoundType Compound
        {
            get
            {
                if (_compound is null)
                {
                    if (Class == H5DataTypeClass.Compound)
                        _compound = new CompoundType(
                            _dataType.Properties.Cast<CompoundPropertyDescription>().ToArray());

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.Compound}.");
                }

                return _compound;
            }
        }

        /// <summary>
        /// Gets the reference data type.
        /// </summary>
        public ReferenceType Reference
        {
            get
            {
                if (_reference is null)
                {
                    if (Class == H5DataTypeClass.Reference)
                        _reference = new ReferenceType();

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.Reference}.");
                }

                return _reference;
            }
        }

        /// <summary>
        /// Gets the enumeration data type.
        /// </summary>

        public EnumerationType Enumeration
        {
            get
            {
                if (_enumeration is null)
                {
                    if (Class == H5DataTypeClass.Enumerated)
                        _enumeration = new EnumerationType();

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.Enumerated}.");
                }

                return _enumeration;
            }
        }

        /// <summary>
        /// Gets the variable-length data type.
        /// </summary>
        public VariableLengthType VariableLength
        {
            get
            {
                if (_variableLength is null)
                {
                    if (Class == H5DataTypeClass.Enumerated)
                        _variableLength = new VariableLengthType();

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.VariableLength}.");
                }

                return _variableLength;
            }
        }

        /// <summary>
        /// Gets the array data type.
        /// </summary>
        public ArrayType Array
        {
            get
            {
                if (_array is null)
                {
                    if (Class == H5DataTypeClass.Enumerated)
                        _array = new ArrayType(
                            (ArrayPropertyDescription)_dataType.Properties[0]);

                    else
                        throw new Exception($"This property can only be called for data of class {H5DataTypeClass.Array}.");
                }

                return _array;
            }
        }

        #endregion
    }
}
