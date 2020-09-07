namespace HDF5.NET
{
    public class ObjectHeaderContinuationMessage : Message
    {
        #region Constructors

        public ObjectHeaderContinuationMessage(H5BinaryReader reader, Superblock superblock) : base(reader)
        {
            this.Offset = superblock.ReadOffset(reader);
            this.Length = superblock.ReadLength(reader);
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        #endregion
    }
}
