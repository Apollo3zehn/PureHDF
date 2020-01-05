namespace HDF5.NET
{
    public class LinkInfoMessage
    {
        #region Constructors

        public LinkInfoMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public CreationOrderFlags Flags { get; set; }
        public ulong MaximumCreationIndex { get; set; }
        public ulong FractalHeapAddress { get; set; }
        public ulong BTree2NameIndexAddress { get; set; }
        public ulong BTree2CreationOrderIndexAddress { get; set; }

        #endregion
    }
}
