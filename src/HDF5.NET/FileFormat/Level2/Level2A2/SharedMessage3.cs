namespace HDF5.NET
{
    public class SharedMessage3
    {
        #region Constructors

        public SharedMessage3()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public SharedMessageLocation Type { get; set; }
        public ulong Location { get; set; }

        #endregion
    }
}
