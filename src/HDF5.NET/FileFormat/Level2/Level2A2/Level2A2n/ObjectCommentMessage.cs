namespace HDF5.NET
{
    internal class ObjectCommentMessage : Message
    {
        #region Constructors

        public ObjectCommentMessage(H5BinaryReader reader) : base(reader)
        {
            // comment
            Comment = H5Utils.ReadNullTerminatedString(reader, pad: false);
        }

        #endregion

        #region Properties

        public string Comment { get; set; }

        #endregion
    }
}
