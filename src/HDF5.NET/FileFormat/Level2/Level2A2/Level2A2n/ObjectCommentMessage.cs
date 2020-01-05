using System.IO;

namespace HDF5.NET
{
    public class ObjectCommentMessage : Message
    {
        #region Constructors

        public ObjectCommentMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public string Comment { get; set; }

        #endregion
    }
}
