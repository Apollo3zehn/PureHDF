namespace HDF5.NET
{
    public class SharedMessageTableMessage
    {
        #region Constructors

        public SharedMessageTableMessage()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public ulong SharedObjectHeaderMessageTableAddress { get; set; }
        public byte IndexCount { get; set; }

        #endregion
    }
}
