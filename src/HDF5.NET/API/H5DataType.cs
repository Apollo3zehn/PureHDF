namespace HDF5.NET
{
    public partial class H5DataType
    {
        #region Types

        public class FixedPointType
        {
            private FixedPointBitFieldDescription _bitField;
            private FixedPointPropertyDescription _property;

            internal FixedPointType(
                FixedPointBitFieldDescription bitField, 
                FixedPointPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;

                IsSigned = _bitField.IsSigned;
            }

            public bool IsSigned { get; }
        }

        public class FloatingPointType
        {
            private FloatingPointBitFieldDescription _bitField;
            private FloatingPointPropertyDescription _property;

            internal FloatingPointType(
                FloatingPointBitFieldDescription bitField, 
                FloatingPointPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        public class StringType
        {
            private StringBitFieldDescription _bitField;

            internal StringType(
                StringBitFieldDescription bitField)
            {
                _bitField = bitField;
            }
        }

        public class BitFieldType
        {
            private BitFieldBitFieldDescription _bitField;
            private BitFieldPropertyDescription _property;

            internal BitFieldType(
                BitFieldBitFieldDescription bitField, 
                BitFieldPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        public class OpaqueType
        {
            private OpaqueBitFieldDescription _bitField;
            private OpaquePropertyDescription _property;

            internal OpaqueType(
                OpaqueBitFieldDescription bitField, 
                OpaquePropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        public class CompoundType
        {
            public record CompoundMember(
                string Name,
                int Offset,
                H5DataType Type
            );

            private CompoundBitFieldDescription _bitField;
            private CompoundPropertyDescription[] _properties;

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

            public CompoundMember[] Members { get; }
        }

        public class ReferenceType
        {
            private ReferenceBitFieldDescription _bitField;

            internal ReferenceType(
                ReferenceBitFieldDescription bitField)
            {
                _bitField = bitField;
            }
        }

        public class EnumerationType
        {
            private EnumerationBitFieldDescription _bitField;
            private EnumerationPropertyDescription _property;

            internal EnumerationType(
                EnumerationBitFieldDescription bitField, 
                EnumerationPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        public class VariableLengthType
        {
            private VariableLengthBitFieldDescription _bitField;
            private VariableLengthPropertyDescription _property;

            internal VariableLengthType(
                VariableLengthBitFieldDescription bitField, 
                VariableLengthPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;
            }
        }

        public class ArrayType
        {
            private ArrayBitFieldDescription _bitField;
            private ArrayPropertyDescription _property;

            internal ArrayType(
                ArrayBitFieldDescription bitField, 
                ArrayPropertyDescription property)
            {
                _bitField = bitField;
                _property = property;

                BaseType = new H5DataType(_property.BaseType);
            }

            public H5DataType BaseType { get; }
        }

        #endregion

        #region Fields

        private FixedPointType _fixedPoint;
        private FloatingPointType _floatingPoint;
        private StringType _string;
        private BitFieldType _bitField;
        private OpaqueType _opaque;
        private CompoundType _compound;
        private ReferenceType _reference;
        private EnumerationType _enumeration;
        private VariableLengthType _variableLength;
        private ArrayType _array;

        #endregion

        #region Properties

        public H5DataTypeClass Class => (H5DataTypeClass)_dataType.Class;

        public int Size => (int)_dataType.Size;

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
