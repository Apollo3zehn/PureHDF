using System.IO;

namespace HDF5.NET
{
    public class ObjectReferenceCountMessage : Message
    {
        #region Constructors

        public ObjectReferenceCountMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public uint ReferenceCount { get; set; }

        #endregion
    }
}
