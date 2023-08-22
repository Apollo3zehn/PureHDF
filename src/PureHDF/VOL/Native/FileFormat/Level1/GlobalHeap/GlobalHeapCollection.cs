using System.Text;

namespace PureHDF.VOL.Native;

internal readonly record struct GlobalHeapCollection(
    Dictionary<int, GlobalHeapObject> GlobalHeapObjects
)
{
    private readonly byte _version;
    private readonly ulong _collectionSize;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("GCOL");

    public required byte Version
    {
        readonly get
        {
            return _version;
        }
        init
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(GlobalHeapCollection)} are supported.");

            _version = value;
        }
    }

    public required ulong CollectionSize
    {
        readonly get
        {
            return _collectionSize;
        }
        init
        {
            if (value < 4096)
                throw new FormatException("The minimum global heap collection size is 4096 bytes.");

            _collectionSize = value;
        }
    }

    public static GlobalHeapCollection Decode(NativeReadContext context)
    {
        // TODO: do not decode individual global heap objects and use a Memory<byte> of size 4096 instead

        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // reserved
        driver.ReadBytes(3);

        // collection size
        var collectionSize = superblock.ReadLength(driver);

        // global heap objects
        var globalHeapObjects = new Dictionary<int, GlobalHeapObject>();

        var headerSize = 8UL + superblock.LengthsSize;
        var remaining = collectionSize;

        while (remaining > headerSize)
        {
            var before = driver.Position;
            var globalHeapObject = GlobalHeapObject.Decode(context);

            // Global Heap Object 0 (free space) can appear at the end of the collection.
            if (globalHeapObject.ObjectIndex == 0)
                break;

            globalHeapObjects[globalHeapObject.ObjectIndex] = globalHeapObject;
            var after = driver.Position;
            var consumed = (ulong)(after - before);

            remaining -= consumed;
        }

        return new GlobalHeapCollection(
            GlobalHeapObjects: globalHeapObjects
        )
        {
            Version = version,
            CollectionSize = collectionSize
        };
    }
}