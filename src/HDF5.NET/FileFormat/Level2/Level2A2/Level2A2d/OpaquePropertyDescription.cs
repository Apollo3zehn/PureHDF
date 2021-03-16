namespace HDF5.NET
{
    internal class OpaquePropertyDescription : DatatypePropertyDescription
    {
        #region Constructors

        public OpaquePropertyDescription(H5BinaryReader reader, byte tagByteLength) : base(reader)
        {
            this.Tag = H5Utils
                .ReadFixedLengthString(reader, tagByteLength)
                .TrimEnd('\0');

#warning How to avoid the appended '\0'? Is this caused by C# string passed to HDF5 lib?
        }

        #endregion

        #region Properties

        public string Tag { get; set; }

        #endregion
    }
}