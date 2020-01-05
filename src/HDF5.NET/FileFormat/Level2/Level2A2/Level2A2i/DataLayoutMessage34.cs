namespace HDF5.NET
{
    public class DataLayoutMessage34
    {
        #region Constructors

        public DataLayoutMessage34()
        {
            //
        }

        #endregion

        #region Properties

        public byte Version { get; set; }
        public LayoutClass LayoutClass { get; set; }
        public StoragePropertyDescription Properties { get; set; }


        #endregion
    }
}
