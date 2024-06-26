namespace PureHDF.VOL.Native;

internal class NativeFixedPointType : IFixedPointType
{
    private readonly FixedPointBitFieldDescription _bitField;

    internal NativeFixedPointType(
        FixedPointBitFieldDescription bitField)
    {
        _bitField = bitField;

        IsSigned = _bitField.IsSigned;
    }

    public bool IsSigned { get; }
}

internal class NativeFloatingPointType : IFloatingPointType
{
    //
}

internal class NativeStringType : IStringType
{
    //
}

internal class NativeBitFieldType : IBitFieldType
{
    //
}

internal class NativeOpaqueType : IOpaqueType
{
    internal NativeOpaqueType(
        OpaquePropertyDescription property)
    {
        Tag = property.Tag;
    }

    public string Tag { get; }
}

internal class NativeCompoundType : ICompoundType
{
    private readonly CompoundPropertyDescription[] _properties;

    internal NativeCompoundType(
        CompoundPropertyDescription[] properties)
    {
        _properties = properties;

        Members = _properties
            .Select(property => new CompoundMember(
                property.Name,
                (int)property.MemberByteOffset,
                new NativeDataType(property.MemberTypeMessage)))
            .ToArray();
    }

    public CompoundMember[] Members { get; }
}

internal class NativeReferenceType : IReferenceType
{
    //
}

internal class NativeEnumerationType : IEnumerationType
{
    private readonly EnumerationPropertyDescription _property;

    internal NativeEnumerationType(
        EnumerationPropertyDescription property)
    {
        _property = property;

        BaseType = new NativeDataType(_property.BaseType);
    }

    public IH5DataType BaseType { get; }
}

internal class NativeVariableLengthType : IVariableLengthType
{
    private readonly VariableLengthPropertyDescription _property;

    internal NativeVariableLengthType(
        VariableLengthPropertyDescription property)
    {
        _property = property;

        BaseType = new NativeDataType(_property.BaseType);
    }

    public IH5DataType BaseType { get; }
}

internal class NativeArrayType : IArrayType
{
    private readonly ArrayPropertyDescription _property;

    internal NativeArrayType(
        ArrayPropertyDescription property)
    {
        _property = property;

        BaseType = new NativeDataType(_property.BaseType);
    }

    public IH5DataType BaseType { get; }
}

internal class NativeDataType : IH5DataType
{
    #region Fields

    private readonly DatatypeMessage _dataType;

    private IFixedPointType? _fixedPoint;
    private IFloatingPointType? _floatingPoint;
    private IStringType? _string;
    private IBitFieldType? _bitField;
    private IOpaqueType? _opaque;
    private ICompoundType? _compound;
    private IReferenceType? _reference;
    private IEnumerationType? _enumeration;
    private IVariableLengthType? _variableLength;
    private IArrayType? _array;

    #endregion

    #region Constructors

    internal NativeDataType(DatatypeMessage datatype)
    {
        _dataType = datatype;
    }

    #endregion

    #region Properties

    public H5DataTypeClass Class => (H5DataTypeClass)_dataType.Class;

    public int Size => (int)_dataType.Size;

    public IFixedPointType FixedPoint
    {
        get
        {
            if (_fixedPoint is null)
            {
                if (Class == H5DataTypeClass.FixedPoint)
                    _fixedPoint = new NativeFixedPointType(
                        (FixedPointBitFieldDescription)_dataType.BitField);

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.FixedPoint}.");
            }

            return _fixedPoint;
        }
    }

    public IFloatingPointType FloatingPoint
    {
        get
        {
            if (_floatingPoint is null)
            {
                if (Class == H5DataTypeClass.FloatingPoint)
                    _floatingPoint = new NativeFloatingPointType();

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.FloatingPoint}.");
            }

            return _floatingPoint;
        }
    }

    public IStringType String
    {
        get
        {
            if (_string is null)
            {
                if (Class == H5DataTypeClass.String)
                    _string = new NativeStringType();

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.String}.");
            }

            return _string;
        }
    }

    public IBitFieldType BitField
    {
        get
        {
            if (_bitField is null)
            {
                if (Class == H5DataTypeClass.BitField)
                    _bitField = new NativeBitFieldType();

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.BitField}.");
            }

            return _bitField;
        }
    }

    public IOpaqueType Opaque
    {
        get
        {
            if (_opaque is null)
            {
                if (Class == H5DataTypeClass.Opaque)
                    _opaque = new NativeOpaqueType((OpaquePropertyDescription)_dataType.Properties[0]);

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.Opaque}.");
            }

            return _opaque;
        }
    }

    public ICompoundType Compound
    {
        get
        {
            if (_compound is null)
            {
                if (Class == H5DataTypeClass.Compound)
                    _compound = new NativeCompoundType(
                        _dataType.Properties.Cast<CompoundPropertyDescription>().ToArray());

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.Compound}.");
            }

            return _compound;
        }
    }

    public IReferenceType Reference
    {
        get
        {
            if (_reference is null)
            {
                if (Class == H5DataTypeClass.Reference)
                    _reference = new NativeReferenceType();

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.Reference}.");
            }

            return _reference;
        }
    }

    public IEnumerationType Enumeration
    {
        get
        {
            if (_enumeration is null)
            {
                if (Class == H5DataTypeClass.Enumerated)
                    _enumeration = new NativeEnumerationType(
                        (EnumerationPropertyDescription)_dataType.Properties[0]);
                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.Enumerated}.");
            }

            return _enumeration;
        }
    }

    public IVariableLengthType VariableLength
    {
        get
        {
            if (_variableLength is null)
            {
                if (Class == H5DataTypeClass.VariableLength)
                    _variableLength = new NativeVariableLengthType(
                        (VariableLengthPropertyDescription)_dataType.Properties[0]);

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.VariableLength}.");
            }

            return _variableLength;
        }
    }

    public IArrayType Array
    {
        get
        {
            if (_array is null)
            {
                if (Class == H5DataTypeClass.Array)
                    _array = new NativeArrayType(
                        (ArrayPropertyDescription)_dataType.Properties[0]);

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.Array}.");
            }

            return _array;
        }
    }

    #endregion
}