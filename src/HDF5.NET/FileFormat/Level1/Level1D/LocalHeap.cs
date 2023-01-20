using System.Text;

namespace HDF5.NET
{
    internal class LocalHeap
    {
        #region Fields

        private byte _version;
        private byte[]? _data;
        private H5BinaryReader _reader;

        #endregion

        #region Constructors

        public LocalHeap(H5Context context)
        {
            var (reader, superblock) = context;
            _reader = reader;

            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, LocalHeap.Signature);

            // version
            Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // data segment size
            DataSegmentSize = superblock.ReadLength(reader);

            // free list head offset
            FreeListHeadOffset = superblock.ReadLength(reader);

            // data segment address
            DataSegmentAddress = superblock.ReadOffset(reader);
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("HEAP");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(LocalHeap)} are supported.");

                _version = value;
            }
        }

        public ulong DataSegmentSize { get; set; }
        public ulong FreeListHeadOffset { get; set; }
        public ulong DataSegmentAddress { get; set; }

        public byte[] Data
        {
            get
            {
                if (_data is null)
                {
                    _reader.Seek((long)DataSegmentAddress, SeekOrigin.Begin);
                    _data = _reader.ReadBytes((int)DataSegmentSize);
                }

                return _data;
            }
        }

        #endregion

        #region Methods

        public string GetObjectName(ulong offset)
        {
            var end = Array.IndexOf(Data, (byte)0, (int)offset);
            var bytes = Data[(int)offset..end];

            return Encoding.ASCII.GetString(bytes);           
        }

        #endregion
    }
}
