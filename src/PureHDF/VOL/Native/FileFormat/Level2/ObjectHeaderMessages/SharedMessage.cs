namespace PureHDF.VOL.Native;

internal record class SharedMessage(
    SharedMessageLocation Type,
    ulong Address
    // TODO: implement this
    // FractalHeapId FractalHeapId
)
{
    private byte _version;

    public static SharedMessage Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // H5Oshared.c (H5O__shared_decode)

        // version
        var version = driver.ReadByte();

        // type
        SharedMessageLocation type;

        if (version == 3)
        {
            type = (SharedMessageLocation)driver.ReadByte();
        }

        else
        {
            driver.ReadByte();
            type = SharedMessageLocation.AnotherObjectsHeader;
        }

        // reserved
        if (version == 1)
            driver.ReadBytes(6);

        // address
        ulong address;

        if (version == 1)
        {
            address = superblock.ReadOffset(driver);
        }
        else
        {
            /* If this message is in the heap, copy a heap ID.
             * Otherwise, it is a named datatype, so copy an H5O_loc_t.
             */

            if (type == SharedMessageLocation.SharedObjectHeaderMessageHeap)
            {
                // TODO: implement this
                throw new NotImplementedException("This code path is not yet implemented.");
            }

            else
            {
                address = superblock.ReadOffset(driver);
            }
        }

        return new SharedMessage(
            Type: type,
            Address: address
        )
        {
            Version = version
        };
    }

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!(1 <= value && value <= 3))
                throw new FormatException($"Only version 1 version 2 and version 3 instances of type {nameof(SharedMessage)} are supported.");

            _version = value;
        }
    }

}