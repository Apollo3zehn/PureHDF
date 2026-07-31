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
        if (collectionSize > int.MaxValue)
        {
            throw new NotSupportedException("The collection size is too big.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent((int)collectionSize);
        driver.ReadDataset(buffer.AsSpan()[..(int)collectionSize]);

        var memoryStream = new MemoryStream(buffer);
        var subDriver = new H5StreamDriver(memoryStream, false);
        var subContext = new NativeReadContext(subDriver, superblock)
        {
            ReadOptions = context.ReadOptions,
            File = context.File,
        };

        // global heap objects
        var globalHeapObjects = new Dictionary<int, GlobalHeapObject>();

        var headerSize = 8UL + superblock.LengthsSize;
        var remaining = collectionSize - headerSize;

        while (remaining > headerSize)
        {
            var before = subDriver.Position;
            var globalHeapObject = GlobalHeapObject.Decode(subContext);

            // Global Heap Object 0 (free space) can appear at the end of the collection.
            if (globalHeapObject.ObjectIndex == 0)
                break;

            globalHeapObjects[globalHeapObject.ObjectIndex] = globalHeapObject;
            var after = subDriver.Position;
            var consumed = (ulong)(after - before);

            remaining -= consumed;
        }

        ArrayPool<byte>.Shared.Return(buffer);

        return new GlobalHeapCollection(
GlobalHeapObjects: globalHeapObjects
)
        {
            Version = version,
            CollectionSize = collectionSize
        };
    }
}