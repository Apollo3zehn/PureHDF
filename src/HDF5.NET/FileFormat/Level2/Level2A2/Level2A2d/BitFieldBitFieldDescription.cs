namespace HDF5.NET
{
    public class BitFieldBitFieldDescription : DatatypeBitFieldDescription
    {
        #region Constructors

        public BitFieldBitFieldDescription()
        {
            //
        }

        #endregion

        #region Properties

        public ByteOrder ByteOrder { get; set; }
        public byte PaddingType { get; set; }

        #endregion
    }
}