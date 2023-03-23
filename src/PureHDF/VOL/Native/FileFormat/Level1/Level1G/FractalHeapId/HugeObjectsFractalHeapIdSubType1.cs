using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PureHDF.VOL.Native;

internal class HugeObjectsFractalHeapIdSubType1 : FractalHeapId
{
    #region Fields

    private NativeContext _context;
    private readonly FractalHeapHeader _heapHeader;

    #endregion

    #region Constructors

    internal HugeObjectsFractalHeapIdSubType1(NativeContext context, H5DriverBase localDriver, FractalHeapHeader header)
    {
        _context = context;
        _heapHeader = header;

        // BTree2 key
        BTree2Key = Utils.ReadUlong(localDriver, header.HugeIdsSize);
    }

    #endregion

    #region Properties

    public ulong BTree2Key { get; set; }

    #endregion

    #region Methods

    public override T Read<T>(Func<H5DriverBase, T> func, [AllowNull] ref List<BTree2Record01> record01Cache)
    {
        var driver = _context.Driver;

        // huge objects b-tree v2
        if (record01Cache is null)
        {
            driver.Seek((long)_heapHeader.HugeObjectsBTree2Address, SeekOrigin.Begin);
            var hugeBtree2 = new BTree2Header<BTree2Record01>(_context, DecodeRecord01);
            record01Cache = hugeBtree2.EnumerateRecords().ToList();
        }

        var hugeRecord = record01Cache.FirstOrDefault(record => record.HugeObjectId == BTree2Key);
        driver.Seek((long)hugeRecord.HugeObjectAddress, SeekOrigin.Begin);

        return func(driver);
    }

    #endregion

    #region Methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BTree2Record01 DecodeRecord01() => new(_context);

    #endregion
}