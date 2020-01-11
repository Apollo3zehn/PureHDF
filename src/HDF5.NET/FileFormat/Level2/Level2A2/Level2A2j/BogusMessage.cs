using System;
using System.IO;

namespace HDF5.NET
{
    public class BogusMessage : Message
    {
        #region Fields

        private uint _bogusValue;

        #endregion

        #region Constructors

        public BogusMessage(BinaryReader reader) : base(reader)
        {
            this.BogusValue = reader.ReadUInt32();
        }

        #endregion

        #region Properties

        public uint BogusValue
        {
            get
            {
                return _bogusValue;
            }
            set
            {
                if (value.ToString("X") != "deadbeef")
                    throw new FormatException($"The bogus value of the {nameof(BogusMessage)} instance is invalid.");

                _bogusValue = value;
            }
        }

        #endregion
    }
}
