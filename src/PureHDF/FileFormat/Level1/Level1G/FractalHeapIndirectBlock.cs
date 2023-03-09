using System.Text;

namespace PureHDF
{
    internal class FractalHeapIndirectBlock
    {
        #region Fields

        private H5Context _context;
        private byte _version;

        #endregion

        #region Constructors

        public FractalHeapIndirectBlock(H5Context context, FractalHeapHeader header, uint rowCount)
        {
            var (driver, superblock) = context;
            _context = context;

            RowCount = rowCount;

            // signature
            var signature = driver.ReadBytes(4);
            Utils.ValidateSignature(signature, FractalHeapIndirectBlock.Signature);

            // version
            Version = driver.ReadByte();

            // heap header address
            HeapHeaderAddress = superblock.ReadOffset(driver);

            // block offset
            var blockOffsetFieldSize = (int)Math.Ceiling(header.MaximumHeapSize / 8.0);
            BlockOffset = Utils.ReadUlong(driver, (ulong)blockOffsetFieldSize);

            // H5HFcache.c (H5HF__cache_iblock_deserialize)
            var length = rowCount * header.TableWidth;
            Entries = new FractalHeapEntry[length];

            for (uint i = 0; i < Entries.Length; i++)
            {
                /* Decode child block address */
                Entries[i].Address = superblock.ReadOffset(driver);

                /* Check for heap with I/O filters */
                if (header.IOFilterEncodedLength > 0)
                {
                    /* Decode extra information for direct blocks */
                    if (i < (header.MaxDirectRows * header.TableWidth))
                    {
                        /* Size of filtered direct block */
                        Entries[i].FilteredSize = superblock.ReadLength(driver);

                        /* I/O filter mask for filtered direct block */
                        Entries[i].FilterMask = driver.ReadUInt32();
                    }
                }


                /* Count child blocks */
                if (!superblock.IsUndefinedAddress(Entries[i].Address))
                {
                    ChildCount++;
                    MaxChildIndex = i;
                }
            }

            // checksum
            Checksum = driver.ReadUInt32();
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FHIB");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(FractalHeapIndirectBlock)} are supported.");

                _version = value;
            }
        }

        public ulong HeapHeaderAddress { get; set; }
        public ulong BlockOffset { get; set; }
        public uint Checksum { get; set; }

        public FractalHeapHeader HeapHeader
        {
            get
            {
                _context.Driver.Seek((long)HeapHeaderAddress, SeekOrigin.Begin);
                return new FractalHeapHeader(_context);
            }
        }

        public FractalHeapEntry[] Entries { get; private set; }
        public uint RowCount { get; }
        public uint ChildCount { get; }
        public uint MaxChildIndex { get; }

        #endregion
    }
}
