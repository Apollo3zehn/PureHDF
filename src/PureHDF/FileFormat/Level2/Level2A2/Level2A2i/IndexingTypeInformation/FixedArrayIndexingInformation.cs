namespace PureHDF
{
    internal class FixedArrayIndexingInformation : IndexingInformation
    {
        #region Constructors

        public FixedArrayIndexingInformation(H5DriverBase driver)
        {
            // page bits
            PageBits = driver.ReadByte();

            if (PageBits == 0)
                throw new Exception("Invalid fixed array creation parameter.");
        }

        #endregion

        #region Properties

        public byte PageBits { get; set; }

        #endregion
    }
}
