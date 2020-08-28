using System;
using System.IO;

namespace HDF5.NET
{
    public abstract class FractalHeapId : FileBlock
    {
        #region Constructors

        public FractalHeapId(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Methods

        public static FractalHeapId Construct(BinaryReader reader, Superblock superblock, ulong offsetByteCount, ulong lengthByteCount)
        {
            var firstByte = reader.ReadByte();

            // bits 6-7
            var version = (byte)((firstByte & 0xB0) >> 6);

            if (version != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapId)} are supported.");

            // bits 4-5
            var type = (FractalHeapIdType)((firstByte & 0x30) >> 4);

#warning: How to determine correct subtype?
            return (FractalHeapId)(type switch
            {
                FractalHeapIdType.Managed   => new ManagedObjectsFractalHeapId(reader, offsetByteCount, lengthByteCount),
                FractalHeapIdType.Huge      => new HugeObjectsFractalHeapIdSubType1And2(reader, superblock),
                FractalHeapIdType.Tiny      => new TinyObjectsFractalHeapIdSubType1(reader, firstByte),
                _                           => throw new Exception($"Unknown heap ID type '{type}'.")
            });
        }

        #endregion
    }
}
