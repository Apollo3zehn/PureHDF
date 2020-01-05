namespace HDF5.NET
{
    public class ObjectHeaderContinuationMessage
    {
        #region Constructors

        public ObjectHeaderContinuationMessage()
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
