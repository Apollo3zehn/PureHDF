namespace HDF5.NET
{
    internal class ObjectHeaderContinuationMessage : Message
    {
        #region Constructors

        public ObjectHeaderContinuationMessage(H5Context context)
        {
            var (reader, superblock) = context;

            Offset = superblock.ReadOffset(reader);
            Length = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        #endregion
    }
}
