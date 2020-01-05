namespace HDF5.NET
{
    public class ExtensibleArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public ExtensibleArrayIndexingInformation()
        {
            //
        }

        #endregion

        #region Properties

        public byte MaxBits { get; set; }
        public byte IndexElements { get; set; }
        public byte MinPointers { get; set; }
        public byte MinElements { get; set; }
        public ushort PageBits { get; set; }

        #endregion
    }
}
