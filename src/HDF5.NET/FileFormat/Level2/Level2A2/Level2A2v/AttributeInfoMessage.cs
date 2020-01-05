using System.IO;

namespace HDF5.NET
{
    public class AttributeInfoMessage : Message
    {
        #region Constructors

        public AttributeInfoMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public CreationOrderFlags Flags { get; set; } 
        public ushort MaximumCreationIndex { get; set; }
        public ulong FractalHeapAddress { get; set; }
        public ulong BTree2NameIndexAddress { get; set; }
        public ulong BTree2CreationOrderIndexAddress { get; set; }

        #endregion
    }
}
