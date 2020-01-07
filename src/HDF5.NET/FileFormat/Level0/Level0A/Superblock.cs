using System;
using System.IO;

namespace HDF5.NET
{
    public abstract class Superblock : FileBlock
    {
        #region Fields

        private byte _offsetsSize;
        private byte _lengthsSize;

        #endregion

        #region Constructors

        public Superblock(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public static byte[] FormatSignature { get; set; } = new byte[] { 0x89, 0x48, 0x44, 0x46, 0x0d, 0x0a, 0x1a, 0x0a };

        public byte SuperBlockVersion { get; set; }

        public byte OffsetsSize
        {
            get
            {
                return _offsetsSize;
            }
            set
            {
                if (!(1 <= value && value <= 8 && H5Utils.IsPowerOfTwo(value)))
                    throw new NotSupportedException("Superblock offsets size must be a power of two and in the range of 1..8.");

                _offsetsSize = value;
            }
        }

        public byte LengthsSize
        {
            get
            {
                return _lengthsSize;
            }
            set
            {
                if (!(1 <= value && value <= 8 && H5Utils.IsPowerOfTwo(value)))
                    throw new NotSupportedException("Superblock lengths size must be a power of two and in the range of 1..8.");

                _lengthsSize = value;
            }
        }

        public FileConsistencyFlags FileConsistencyFlags { get; set; }

        #endregion

        #region Methods

        public ulong ReadOffset()
        {
            return this.ReadUlong(this.OffsetsSize);
        }

        public ulong ReadLength()
        {
            return this.ReadUlong(this.LengthsSize);
        }

        #endregion
    }
}
