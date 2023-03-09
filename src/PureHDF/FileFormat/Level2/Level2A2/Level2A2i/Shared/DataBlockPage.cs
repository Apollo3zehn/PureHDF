namespace PureHDF
{
    internal class DataBlockPage<T>
    {
        #region Constructors

        public DataBlockPage(H5DriverBase driver, ulong elementCount, Func<H5DriverBase, T> decode)
        {
            // elements
            Elements = Enumerable
                .Range(0, (int)elementCount)
                .Select(i => decode(driver))
                .ToArray();

            // checksum
            Checksum = driver.ReadUInt32();
        }

        #endregion

        #region Properties

        public T[] Elements { get; }

        public ulong Checksum { get; }

        #endregion
    }
}
