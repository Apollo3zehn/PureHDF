using System.IO;

namespace HDF5.NET
{
    public class ObjectHeaderContinuationMessage : Message
    {
        #region Constructors

        public ObjectHeaderContinuationMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public ulong Offset { get; set; }
        public ulong Length { get; set; }

        #endregion
    }
}
