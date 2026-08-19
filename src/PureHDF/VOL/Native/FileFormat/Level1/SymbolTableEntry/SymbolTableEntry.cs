namespace PureHDF.VOL.Native;

internal readonly record struct SymbolTableEntry(
    ulong LinkNameOffset,
    ulong HeaderAddress,
    CacheType CacheType,
    ScratchPad? ScratchPad
)
{
    public static async ValueTask<SymbolTableEntry> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // link name offset
        var linkNameOffset = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // object header address
        var headerAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // cache type
        var cacheType = (CacheType)await driver.ReadUInt32().ConfigureAwait(false);

        // reserved
        await driver.ReadUInt32().ConfigureAwait(false);

        // scratch pad
        var before = driver.Position;

        ScratchPad? scratchPad;

        switch (cacheType)
        {
            case CacheType.NoCache:
                scratchPad = null;
                break;

            case CacheType.ObjectHeader:
                scratchPad = await ObjectHeaderScratchPad.Decode(context).ConfigureAwait(false);
                break;

            case CacheType.SymbolicLink:
                scratchPad = await SymbolicLinkScratchPad.Decode(driver).ConfigureAwait(false);
                break;

            default:
                throw new NotSupportedException();
        }

        var after = driver.Position;
        var length = after - before;

        // read as many bytes as needed to read a total of 16 bytes, even if the scratch pad is not used
        await driver.ReadBytes((int)(16 - length)).ConfigureAwait(false);

        return new SymbolTableEntry(
            LinkNameOffset: linkNameOffset,
            HeaderAddress: headerAddress,
            CacheType: cacheType,
            ScratchPad: scratchPad
        );
    }
}