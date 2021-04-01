using System;
using System.Linq;

namespace HDF5.NET
{
    public partial class H5DataType
    {
        #region Types

        public class FixedPointType
        {
            #region Fields

            private FixedPointBitFieldDescription _bitField;
            private FixedPointPropertyDescription _propertyDescription;

            #endregion

            #region Constructors

            internal FixedPointType(FixedPointBitFieldDescription bitField, FixedPointPropertyDescription propertyDescription)
            {
                _bitField = bitField;
                _propertyDescription = propertyDescription;
            }

            #endregion

            #region Properties

            public bool IsSigned => _bitField.IsSigned;

            #endregion
        }

        #endregion

        #region Fields

        private FixedPointType _fixedPoint;

        #endregion

        #region Properties

        public H5DataTypeClass Class => (H5DataTypeClass)_dataType.Class;

        public uint Size => _dataType.Size;

        public FixedPointType FixedPoint
        {
            get
            {
                if (_fixedPoint is null)
                {
                    if (this.Class == H5DataTypeClass.FixedPoint)
                        _fixedPoint = new FixedPointType(
                            (FixedPointBitFieldDescription)_dataType.BitField, 
                            (FixedPointPropertyDescription)_dataType.Properties.First());

                    else
                        throw new Exception("This property can only be called for fixed point data types.");
                }

                return _fixedPoint;
            }
        }

        #endregion
    }
}
