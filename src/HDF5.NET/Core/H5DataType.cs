namespace HDF5.NET
{
    partial class H5DataType
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
    }
}
