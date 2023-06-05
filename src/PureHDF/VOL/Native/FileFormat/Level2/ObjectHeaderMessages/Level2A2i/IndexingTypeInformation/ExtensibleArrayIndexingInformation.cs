namespace PureHDF.VOL.Native;

internal class ExtensibleArrayIndexingInformation : IndexingInformation
{
    #region Constructors

    public ExtensibleArrayIndexingInformation(H5DriverBase driver)
    {
        // max bit count
        MaxBitCount = driver.ReadByte();

        if (MaxBitCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // index element count
        IndexElementsCount = driver.ReadByte();

        if (IndexElementsCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // min pointer count
        MinPointerCount = driver.ReadByte();

        if (MinPointerCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // min element count
        MinElementsCount = driver.ReadByte();

        if (MinElementsCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");

        // page bit count
        PageBitCount = driver.ReadByte();

        if (PageBitCount == 0)
            throw new Exception("Invalid extensible array creation parameter.");
    }

    #endregion

    #region Properties

    public byte MaxBitCount { get; set; }
    public byte IndexElementsCount { get; set; }
    public byte MinPointerCount { get; set; }
    public byte MinElementsCount { get; set; }
    public ushort PageBitCount { get; set; }

    #endregion
}