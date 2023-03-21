namespace PureHDF.VOL.Native;

internal class SharedMessage
{
    #region Fields

    private byte _version;

    #endregion

    #region Constructors

    public SharedMessage(NativeContext context)
    {
        var (driver, superblock) = context;

        // H5Oshared.c (H5O__shared_decode)

        // version
        Version = driver.ReadByte();

        // type
        if (Version == 3)
        {
            Type = (SharedMessageLocation)driver.ReadByte();
        }
        else
        {
            driver.ReadByte();
            Type = SharedMessageLocation.AnotherObjectsHeader;
        }

        // reserved
        if (Version == 1)
            driver.ReadBytes(6);

        // address
        if (Version == 1)
        {
            Address = superblock.ReadOffset(driver);
        }
        else
        {
            /* If this message is in the heap, copy a heap ID.
             * Otherwise, it is a named datatype, so copy an H5O_loc_t.
             */

            if (Type == SharedMessageLocation.SharedObjectHeaderMessageHeap)
            {
                // TODO: implement this
                throw new NotImplementedException("This code path is not yet implemented.");
            }
            else
            {
                Address = superblock.ReadOffset(driver);
            }
        }
    }

    #endregion

    #region Properties

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (!(1 <= value && value <= 3))
                throw new FormatException($"Only version 1 version 2 and version 3 instances of type {nameof(SharedMessage)} are supported.");

            _version = value;
        }
    }

    public SharedMessageLocation Type { get; set; }

    public ulong Address { get; set; }

    // TODO: implement this
    // public FractalHeapId FractalHeapId { get; set; }

    #endregion
}