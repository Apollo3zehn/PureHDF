namespace PureHDF
{
    internal class FractalHeapSharedMessageRecord : SharedMessageRecord
    {
        #region Constructors

        public FractalHeapSharedMessageRecord(H5DriverBase driver) : base(driver)
        {
            // hash value
            HashValue = driver.ReadUInt32();

            // reference count
            ReferenceCount = driver.ReadUInt32();

            // fractal heap ID
            FractalHeapId = driver.ReadUInt64();
        }

        #endregion

        #region Properties

        public uint HashValue { get; set; }
        public uint ReferenceCount { get; set; }
        public ulong FractalHeapId { get; set; }

        #endregion
    }
}
