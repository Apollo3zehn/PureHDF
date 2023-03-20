using static PureHDF.IH5DataType;

namespace PureHDF.VOL.Native;

internal class H5DataType : IH5DataType
{
    #region Fields

    private readonly DatatypeMessage _dataType;

    private FixedPointType? _fixedPoint;
    private FloatingPointType? _floatingPoint;
    private StringType? _string;
    private BitFieldType? _bitField;
    private OpaqueType? _opaque;
    private CompoundType? _compound;
    private ReferenceType? _reference;
    private EnumerationType? _enumeration;
    private VariableLengthType? _variableLength;
    private ArrayType? _array;

    #endregion

    #region Constructors

    internal H5DataType(DatatypeMessage datatype)
    {
        _dataType = datatype;
    }

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
                        (FixedPointBitFieldDescription)_dataType.BitField);

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
                    _floatingPoint = new FloatingPointType();

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
                    _string = new StringType();

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
                    _bitField = new BitFieldType();

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
                    _opaque = new OpaqueType((OpaquePropertyDescription)_dataType.Properties[0]);

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
                    _reference = new ReferenceType();

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
                    _enumeration = new EnumerationType();

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
                    _variableLength = new VariableLengthType();

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
                        (ArrayPropertyDescription)_dataType.Properties[0]);

                else
                    throw new Exception($"This property can only be called for data of class {H5DataTypeClass.Array}.");
            }

            return _array;
        }
    }

    #endregion
}