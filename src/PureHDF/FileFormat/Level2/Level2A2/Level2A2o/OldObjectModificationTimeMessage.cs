namespace PureHDF
{
    internal class OldObjectModificationTimeMessage : Message
    {
        #region Constructors

        public OldObjectModificationTimeMessage(H5BaseReader reader)
        {
            // date / time
            Year = int.Parse(ReadUtils.ReadFixedLengthString(reader, 4));
            Month = int.Parse(ReadUtils.ReadFixedLengthString(reader, 2));
            DayOfMonth = int.Parse(ReadUtils.ReadFixedLengthString(reader, 2));
            Hour = int.Parse(ReadUtils.ReadFixedLengthString(reader, 2));
            Minute = int.Parse(ReadUtils.ReadFixedLengthString(reader, 2));
            Second = int.Parse(ReadUtils.ReadFixedLengthString(reader, 2));

            // reserved
            reader.ReadBytes(2);
        }

        #endregion

        #region Properties

        public int Year { get; set; }
        public int Month { get; set; }
        public int DayOfMonth { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Second { get; set; }

        #endregion

        #region Methods

        public ObjectModificationMessage ToObjectModificationMessage()
        {
            var dateTime = new DateTime(Year, Month, DayOfMonth, Hour, Minute, Second);
            var secondsAfterUnixEpoch = (uint)((DateTimeOffset)dateTime).ToUnixTimeSeconds();

            return new ObjectModificationMessage(secondsAfterUnixEpoch);
        }

        #endregion
    }
}
