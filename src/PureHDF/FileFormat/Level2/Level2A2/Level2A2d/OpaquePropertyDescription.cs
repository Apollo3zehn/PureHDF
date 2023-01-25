namespace PureHDF
{
    internal class OpaquePropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public OpaquePropertyDescription(H5BaseReader reader, byte tagByteLength)
        {
            Tag = H5ReadUtils
                .ReadFixedLengthString(reader, tagByteLength)
                .TrimEnd('\0');

            // TODO: How to avoid the appended '\0'? Is this caused by C# string passed to HDF5 lib?
        }

        #endregion

        #region Properties

        public string Tag { get; set; }

        #endregion
    }
}