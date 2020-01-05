using System;
using System.IO;

namespace HDF5.NET
{
    public abstract class Superblock : FileBlock
    {
        #region Constructors

        public Superblock(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public static byte[] FormatSignature { get; set; } = new byte[] { 0x89, 0x48, 0x44, 0x46, 0x0d, 0x0a, 0x1a, 0x0a };

        public byte SuperBlockVersion { get; set; }
        public byte OffsetsSize { get; set; }
        public byte LengthsSize { get; set; }
        public FileConsistencyFlags FileConsistencyFlags { get; set; }

        #endregion

        #region Methods

        public override void Validate()
        {
            if (!(1 <= this.OffsetsSize && this.OffsetsSize <= 8 && H5Utils.IsPowerOfTwo(this.OffsetsSize)))
                throw new NotSupportedException("Superblock offsets size must be a power of two and in the range of 1..8.");

            if (!(1 <= this.LengthsSize && this.LengthsSize <= 8 && H5Utils.IsPowerOfTwo(this.LengthsSize)))
                throw new NotSupportedException("Superblock lengths size must be a power of two and in the range of 1..8.");
        }

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
