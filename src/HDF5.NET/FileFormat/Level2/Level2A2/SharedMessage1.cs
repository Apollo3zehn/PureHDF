namespace HDF5.NET
{
    public class SharedMessage1
    {
        #region Constructors

        public SharedMessage1()
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
