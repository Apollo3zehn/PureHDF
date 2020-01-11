using System.IO;

namespace HDF5.NET
{
    public class OldObjectModificationTimeMessage : Message
    {
        #region Constructors

        public OldObjectModificationTimeMessage(BinaryReader reader) : base(reader)
        {
            // date / time
            this.Year = H5Utils.ReadFixedLengthString(reader, 4);
            this.Month = H5Utils.ReadFixedLengthString(reader, 2);
            this.DayOfMonth = H5Utils.ReadFixedLengthString(reader, 2);
            this.Hour = H5Utils.ReadFixedLengthString(reader, 2);
            this.Minute = H5Utils.ReadFixedLengthString(reader, 2);
            this.Second = H5Utils.ReadFixedLengthString(reader, 2);

            // reserved
            reader.ReadBytes(2);
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
