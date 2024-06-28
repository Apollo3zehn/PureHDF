using Hsds.Api;

namespace PureHDF.VOL.Hsds;

internal class HsdsFixedPointType : IFixedPointType
{
    internal HsdsFixedPointType(string baseClass)
    {
        var parts = baseClass.Split("_", count: 3);
        IsSigned = parts[2].StartsWith('I');
    }

    public bool IsSigned { get; }
}

internal class HsdsFloatingPointType : IFloatingPointType
{
    //
}

internal class HsdsStringType : IStringType
{
    //
}

internal class HsdsBitFieldType : IBitFieldType
{
    //
}

internal class HsdsOpaqueType : IOpaqueType
{
    public string Tag => throw new NotImplementedException();
}

internal class HsdsCompoundType : ICompoundType
{
    internal HsdsCompoundType(
        IReadOnlyList<TypeTypeFieldsType> fields)
    {
        Members = fields
            .Select(field => new CompoundMember(
                field.Name,
                Offset: default, // TODO implement
                new HsdsDataType(field.Type)))
            .ToArray();
    }

    public CompoundMember[] Members { get; }
}

internal class HsdsReferenceType : IReferenceType
{
    //
}

internal class HsdsEnumerationType : IEnumerationType
{
    public IH5DataType BaseType => throw new NotImplementedException();

    public IDictionary<string, T> GetMembers<T>() where T : unmanaged => throw new NotImplementedException();
}

internal class HsdsVariableLengthType : IVariableLengthType
{
    public IH5DataType BaseType => throw new NotImplementedException();
}

internal class HsdsArrayType : IArrayType
{
    public IH5DataType BaseType => throw new NotImplementedException();
}

internal class HsdsDataType : IH5DataType
{
    private readonly TypeType _type;

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

    public HsdsDataType(TypeType type)
    {
        _type = type;

        Class = type.Class switch
        {
            "H5T_COMPOUND" => H5DataTypeClass.Compound,
            "H5T_FLOAT" => H5DataTypeClass.FloatingPoint,
            "H5T_INTEGER" => H5DataTypeClass.FixedPoint,
            _ => throw new Exception($"Unknown data type {type.Class}.")
        };
    }

    public H5DataTypeClass Class { get; }

    public int Size => throw new NotImplementedException("This property is not implemented in the HSDS VOL connector.");

    public IFixedPointType FixedPoint
    {
        get
        {
            if (_fixedPoint is null)
            {
                if (Class == H5DataTypeClass.FixedPoint)
                    _fixedPoint = new HsdsFixedPointType(_type.Base!);

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
                    _floatingPoint = new HsdsFloatingPointType();

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
                    _string = new HsdsStringType();

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
                    _bitField = new HsdsBitFieldType();

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
                    _opaque = new HsdsOpaqueType();

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
                    _compound = new HsdsCompoundType(_type.Fields!);

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
                    _reference = new HsdsReferenceType();

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
                    _enumeration = new HsdsEnumerationType();

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
                    _variableLength = new HsdsVariableLengthType();

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
                    _array = new HsdsArrayType();

                else
                    throw new Exception($"This property can only be accessed for data of class {H5DataTypeClass.Array}.");
            }

            return _array;
        }
    }
}