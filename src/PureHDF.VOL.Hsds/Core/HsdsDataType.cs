namespace PureHDF.VOL.Hsds;

internal class HsdsDataType : IH5DataType
{
    public H5DataTypeClass Class => throw new NotImplementedException();

    public int Size => throw new NotImplementedException();

    public IH5DataType.FixedPointType FixedPoint => throw new NotImplementedException();

    public IH5DataType.FloatingPointType FloatingPoint => throw new NotImplementedException();

    public IH5DataType.StringType String => throw new NotImplementedException();

    public IH5DataType.BitFieldType BitField => throw new NotImplementedException();

    public IH5DataType.OpaqueType Opaque => throw new NotImplementedException();

    public IH5DataType.CompoundType Compound => throw new NotImplementedException();

    public IH5DataType.ReferenceType Reference => throw new NotImplementedException();

    public IH5DataType.EnumerationType Enumeration => throw new NotImplementedException();

    public IH5DataType.VariableLengthType VariableLength => throw new NotImplementedException();

    public IH5DataType.ArrayType Array => throw new NotImplementedException();
}