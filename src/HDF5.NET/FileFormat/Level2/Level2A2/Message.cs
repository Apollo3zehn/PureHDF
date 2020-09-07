namespace HDF5.NET
{
    public abstract class Message : FileBlock
    {
        #region Constructors

        public Message(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
