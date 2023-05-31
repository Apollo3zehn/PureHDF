namespace PureHDF.VOL.Native;

internal readonly record struct SymbolTableEntry(
    ulong LinkNameOffset,
    ulong HeaderAddress,
    CacheType CacheType,
    ScratchPad? ScratchPad
)
{
    public static SymbolTableEntry Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // link name offset
        var linkNameOffset = superblock.ReadOffset(driver);

        // object header address
        var headerAddress = superblock.ReadOffset(driver);

        // cache type
        var cacheType = (CacheType)driver.ReadUInt32();

        // reserved
        driver.ReadUInt32();

        // scratch pad
        var before = driver.Position;

        ScratchPad? scratchPad = cacheType switch
        {
            CacheType.NoCache => null,
            CacheType.ObjectHeader => ObjectHeaderScratchPad.Decode(context),
            CacheType.SymbolicLink => SymbolicLinkScratchPad.Decode(driver),
            _ => throw new NotSupportedException()
        };

        var after = driver.Position;
        var length = after - before;

        // read as many bytes as needed to read a total of 16 bytes, even if the scratch pad is not used
        driver.ReadBytes((int)(16 - length));

        return new SymbolTableEntry(
            LinkNameOffset: linkNameOffset,
            HeaderAddress: headerAddress,
            CacheType: cacheType,
            ScratchPad: scratchPad
        );
    }
}