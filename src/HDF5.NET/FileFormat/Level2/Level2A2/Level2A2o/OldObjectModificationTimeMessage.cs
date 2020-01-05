namespace HDF5.NET
{
    public class OldObjectModificationTimeMessage
    {
        #region Constructors

        public OldObjectModificationTimeMessage()
        {
            //
        }

        #endregion

        #region Properties

        public string Year { get; set; }
        public string Month { get; set; }
        public string DayOfMonth { get; set; }
        public string Hour { get; set; }
        public string Minute { get; set; }
        public string Second { get; set; }

        #endregion
    }
}
