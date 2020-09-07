namespace HDF5.NET
{
    public class TimePropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public TimePropertyDescription(H5BinaryReader reader) : base(reader)
        {
            this.BitPrecision = reader.ReadUInt16();
        }

        #endregion

        #region Properties

        public ushort BitPrecision { get; set; }

        #endregion
    }
}