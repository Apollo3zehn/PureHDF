using System.IO;

namespace HDF5.NET
{
    public class OldObjectModificationTimeMessage : Message
    {
        #region Constructors

        public OldObjectModificationTimeMessage(BinaryReader reader) : base(reader)
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
