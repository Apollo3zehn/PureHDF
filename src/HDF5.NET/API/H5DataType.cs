namespace HDF5.NET
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
            private readonly FixedPointPropertyDescription _property;

            internal FixedPointType(
                FixedPointBitFieldDescription bitField,
                FixedPointPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;

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
            private readonly FloatingPointBitFieldDescription _bitField;
            private readonly FloatingPointPropertyDescription _property;

            internal FloatingPointType(
                FloatingPointBitFieldDescription bitField,
                FloatingPointPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        /// <summary>
        /// The string data type.
        /// </summary>
        public class StringType
        {
            private readonly StringBitFieldDescription _bitField;

            internal StringType(
                StringBitFieldDescription bitField)
            {
                _bitField = bitField;
            }
        }

        /// <summary>
        /// The bitfield data type.
        /// </summary>
        public class BitFieldType
        {
            private readonly BitFieldBitFieldDescription _bitField;
            private readonly BitFieldPropertyDescription _property;

            internal BitFieldType(
                BitFieldBitFieldDescription bitField,
                BitFieldPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        /// <summary>
        /// The opaque data type.
        /// </summary>
        public class OpaqueType
        {
            private readonly OpaqueBitFieldDescription _bitField;
            private readonly OpaquePropertyDescription _property;

            internal OpaqueType(
                OpaqueBitFieldDescription bitField,
                OpaquePropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
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

            private readonly CompoundBitFieldDescription _bitField;
            private readonly CompoundPropertyDescription[] _properties;

            internal CompoundType(
                CompoundBitFieldDescription bitField,
                CompoundPropertyDescription[] properties)
            {
                _bitField = bitField;
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
            private readonly ReferenceBitFieldDescription _bitField;

            internal ReferenceType(
                ReferenceBitFieldDescription bitField)
            {
                _bitField = bitField;
            }
        }

        /// <summary>
        /// The enumeration data type.
        /// </summary>
        public class EnumerationType
        {
            private readonly EnumerationBitFieldDescription _bitField;
            private readonly EnumerationPropertyDescription _property;

            internal EnumerationType(
                EnumerationBitFieldDescription bitField,
                EnumerationPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        /// <summary>
        /// The variable-length data type.
        /// </summary>
        public class VariableLengthType
        {
            private readonly VariableLengthBitFieldDescription _bitField;
            private readonly VariableLengthPropertyDescription _property;

            internal VariableLengthType(
                VariableLengthBitFieldDescription bitField,
                VariableLengthPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        /// <summary>
        /// The array data type.
        /// </summary>
        public class ArrayType
        {
            private readonly ArrayBitFieldDescription _bitField;
            private readonly ArrayPropertyDescription _property;

            internal ArrayType(
                ArrayBitFieldDescription bitField,
                ArrayPropertyDescription property)
            {
                _bitField = bitField;
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
                            (FixedPointBitFieldDescription)_dataType.BitField,
                            (FixedPointPropertyDescription)_dataType.Properties[0]);

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
                        _floatingPoint = new FloatingPointType(
                            (FloatingPointBitFieldDescription)_dataType.BitField,
                            (FloatingPointPropertyDescription)_dataType.Properties[0]);

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
                        _string = new StringType(
                            (StringBitFieldDescription)_dataType.BitField);

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
                        _bitField = new BitFieldType(
                            (BitFieldBitFieldDescription)_dataType.BitField,
                            (BitFieldPropertyDescription)_dataType.Properties[0]);

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
                        _opaque = new OpaqueType(
                            (OpaqueBitFieldDescription)_dataType.BitField,
                            (OpaquePropertyDescription)_dataType.Properties[0]);

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
                            (CompoundBitFieldDescription)_dataType.BitField,
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
                        _reference = new ReferenceType(
                            (ReferenceBitFieldDescription)_dataType.BitField);

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
                        _enumeration = new EnumerationType(
                            (EnumerationBitFieldDescription)_dataType.BitField,
                            (EnumerationPropertyDescription)_dataType.Properties[0]);

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
                        _variableLength = new VariableLengthType(
                            (VariableLengthBitFieldDescription)_dataType.BitField,
                            (VariableLengthPropertyDescription)_dataType.Properties[0]);

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
                            (ArrayBitFieldDescription)_dataType.BitField,
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
