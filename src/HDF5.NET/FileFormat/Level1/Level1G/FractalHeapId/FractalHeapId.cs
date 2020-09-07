using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        public static FractalHeapId Construct(BinaryReader reader, Superblock superblock, BinaryReader localReader, FractalHeapHeader header)
        {
            var firstByte = localReader.ReadByte();

            // bits 6-7
            var version = (byte)((firstByte & 0xB0) >> 6);

            if (version != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapId)} are supported.");

            // bits 4-5
            var type = (FractalHeapIdType)((firstByte & 0x30) >> 4);

            // offset and length Size (for managed objects fractal heap id)
            var offsetSize = (ulong)Math.Ceiling(header.MaximumHeapSize / 8.0);
#warning Is -1 correct?
            var lengthSize = H5Utils.FindMinByteCount(header.MaximumDirectBlockSize - 1);

            // H5HF.c (H5HF_op)
            return (FractalHeapId)((type, header.HugeIdsAreDirect, header.IOFilterEncodedLength, header.TinyObjectsAreExtended) switch
            {
                (FractalHeapIdType.Managed, _, _, _)    => new ManagedObjectsFractalHeapId(reader, localReader, header, offsetSize, lengthSize),

                // H5HFhuge.c (H5HF__huge_op_real)
                (FractalHeapIdType.Huge, false, 0, _)   => new HugeObjectsFractalHeapIdSubType1(reader, superblock, localReader, header),
                (FractalHeapIdType.Huge, false, _, _)   => new HugeObjectsFractalHeapIdSubType2(reader, superblock, localReader, header),
                (FractalHeapIdType.Huge, true, 0, _)    => new HugeObjectsFractalHeapIdSubType3(reader, superblock, localReader),
                (FractalHeapIdType.Huge, true, _, _)    => new HugeObjectsFractalHeapIdSubType4(reader, superblock, localReader),

                // H5HFtiny.c (H5HF_tiny_op_real)
                (FractalHeapIdType.Tiny, _, _, false)   => new TinyObjectsFractalHeapIdSubType1(localReader, firstByte),
                (FractalHeapIdType.Tiny, _, _, true)    => new TinyObjectsFractalHeapIdSubType2(localReader, firstByte),

                // default
                _                                       => throw new Exception($"Unknown heap ID type '{type}'.")
            });
        }

        public abstract T Read<T>(Func<BinaryReader, T> func, [AllowNull]ref IEnumerable<BTree2Record01> record01Cache);

        #endregion
    }
}
