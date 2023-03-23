using System.Diagnostics.CodeAnalysis;

namespace PureHDF.VOL.Native;

internal class HugeObjectsFractalHeapIdSubType4 : FractalHeapId
{
    #region Constructors

    public HugeObjectsFractalHeapIdSubType4(Superblock superblock, H5DriverBase localDriver)
    {
        // address
        Address = superblock.ReadOffset(localDriver);

        // length
        Length = superblock.ReadLength(localDriver);

        // filter mask
        FilterMask = localDriver.ReadUInt32();

        // de-filtered size
        DeFilteredSize = superblock.ReadLength(localDriver);
    }

    #endregion

    #region Properties

    public ulong Address { get; set; }
    public ulong Length { get; set; }
    public uint FilterMask { get; set; }
    public ulong DeFilteredSize { get; set; }

    #endregion

    #region Methods

    public override T Read<T>(Func<H5DriverBase, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        throw new Exception("Filtered data is not yet supported.");
    }

    #endregion
}