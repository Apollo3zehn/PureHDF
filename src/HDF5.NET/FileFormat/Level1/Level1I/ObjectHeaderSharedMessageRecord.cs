namespace HDF5.NET
{
    public class ObjectHeaderSharedMessageRecord
    {
        #region Constructors

        public ObjectHeaderSharedMessageRecord()
        {
            //
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; set; }
        public uint HashValue { get; set; }
        public MessageType MessageType { get; set; }
        public ushort CreationIndex { get; set; }
        public ulong ObjectHeaderAddress { get; set; }

        #endregion
    }
}