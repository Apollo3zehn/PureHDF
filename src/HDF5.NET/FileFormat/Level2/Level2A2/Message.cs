namespace HDF5.NET
{
    internal abstract class Message : FileReader
    {
        #region Constructors

        public Message(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion
    }
}
