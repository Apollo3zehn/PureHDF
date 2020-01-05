using System;
using System.IO;

namespace HDF5.NET
{
    public class DatatypeMessage : Message
    {
        #region Constructors

        public DatatypeMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public byte ClassVersion { get; set; }

        public DatatypeBitFieldDescription BitFieldDescription { get; set; }

        public ulong Size { get; set; }
        public DatatypePropertyDescription Properties { get; set; }

        public byte Version
        {
            get
            {
                return (byte)(this.ClassVersion & 0x0F);
            }
            set
            {
                if (value > 0x0F)
                    throw new Exception("The version number must be <= 15.");

                this.ClassVersion = (byte)(this.ClassVersion & 0xF0);
                this.ClassVersion |= value;
            }
        }

        public DatatypeMessageClass Class
        {
            get
            {
                return (DatatypeMessageClass)((this.ClassVersion & 0xF0) >> 4);
            }
            set
            {
                this.ClassVersion = (byte)(this.ClassVersion & 0x0F);
                this.ClassVersion |= (byte)((byte)value << 4);
            }
        }

        #endregion
    }
}
