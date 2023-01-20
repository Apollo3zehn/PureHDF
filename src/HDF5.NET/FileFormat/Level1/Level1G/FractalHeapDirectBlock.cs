using System.Text;

namespace HDF5.NET
{
    internal class FractalHeapDirectBlock
    {
        #region Fields

        private H5Context _context;
        private byte _version;

        #endregion

        #region Constructors

        public FractalHeapDirectBlock(H5Context context, FractalHeapHeader header)
        {            
            var (reader, superblock) = context;
            _context = context;

            var headerSize = 0UL;

            // signature
            var signature = reader.ReadBytes(4);
            headerSize += 4;
            H5Utils.ValidateSignature(signature, FractalHeapDirectBlock.Signature);

            // version
            Version = reader.ReadByte();
            headerSize += 1;

            // heap header address
            HeapHeaderAddress = superblock.ReadOffset(reader);
            headerSize += superblock.OffsetsSize;

            // block offset
            var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
            BlockOffset = H5Utils.ReadUlong(reader, (ulong)blockOffsetFieldSize);
            headerSize += (ulong)blockOffsetFieldSize;

            // checksum
            if (header.Flags.HasFlag(FractalHeapHeaderFlags.DirectBlocksAreChecksummed))
            {
                Checksum = reader.ReadUInt32();
                headerSize += 4;
            }

            HeaderSize = headerSize;
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FHDB");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapDirectBlock)} are supported.");

                _version = value;
            }
        }

        public ulong HeapHeaderAddress { get; set; }
        public ulong BlockOffset { get; set; }
        public uint Checksum { get; set; }

        // TODO: Implement this.
        // public byte[] ObjectData { get; set; }

        public FractalHeapHeader HeapHeader
        {
            get
            {
                _context.Reader.Seek((long)HeapHeaderAddress, SeekOrigin.Begin);
                return new FractalHeapHeader(_context);
            }
        }

        public ulong HeaderSize { get; }

        #endregion
    }
}
