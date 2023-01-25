namespace PureHDF
{
    internal class ObjectCommentMessage : Message
    {
        #region Constructors

        public ObjectCommentMessage(H5BaseReader reader)
        {
            // comment
            Comment = H5ReadUtils.ReadNullTerminatedString(reader, pad: false);
        }

        #endregion

        #region Properties

        public string Comment { get; set; }

        #endregion
    }
}
