using System.Text;

namespace PureHDF
{
    internal class GlobalHeapCollection
    {
        #region Fields

        private byte _version;
        private ulong _collectionSize;

        #endregion

        #region Constructors

        public GlobalHeapCollection(H5Context context)
        {
            var (reader, superblock) = context;

            // signature
            var signature = reader.ReadBytes(4);
            Utils.ValidateSignature(signature, GlobalHeapCollection.Signature);

            // version
            Version = reader.ReadByte();

            // reserved
            reader.ReadBytes(3);

            // collection size
            CollectionSize = superblock.ReadLength(reader);

            // global heap objects
            GlobalHeapObjects = new Dictionary<int, GlobalHeapObject>();

            var headerSize = 8UL + superblock.LengthsSize;
            var remaining = CollectionSize;

            while (remaining > headerSize)
            {
                var before = reader.Position;
                var globalHeapObject = new GlobalHeapObject(context);

                // Global Heap Object 0 (free space) can appear at the end of the collection.
                if (globalHeapObject.HeapObjectIndex == 0)
                    break;

                GlobalHeapObjects[globalHeapObject.HeapObjectIndex] = globalHeapObject;
                var after = reader.Position;
                var consumed = (ulong)(after - before);

                remaining -= consumed;
            }
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("GCOL");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(GlobalHeapCollection)} are supported.");

                _version = value;
            }
        }

        public ulong CollectionSize
        {
            get
            {
                return _collectionSize;
            }
            set
            {
                if (value < 4096)
                    throw new FormatException("The minimum global heap collection size is 4096 bytes.");

                _collectionSize = value;
            }
        }

        public Dictionary<int, GlobalHeapObject> GlobalHeapObjects { get; set; }

        #endregion
    }
}
