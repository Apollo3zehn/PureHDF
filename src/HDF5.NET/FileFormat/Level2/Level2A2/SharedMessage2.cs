namespace HDF5.NET
{
    public class SharedMessage2
    {
        #region Constructors

        public SharedMessage2()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public byte Type { get; set; }
        public ulong Address { get; set; }

        #endregion
    }
}
