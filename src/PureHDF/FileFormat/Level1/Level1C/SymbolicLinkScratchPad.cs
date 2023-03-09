namespace PureHDF;

internal class SymbolicLinkScratchPad : ScratchPad
{
    #region Constructors

    public SymbolicLinkScratchPad(H5DriverBase driver)
    {
        LinkValueOffset = driver.ReadUInt32();
    }

    #endregion

    #region Properties

    public uint LinkValueOffset { get; set; }

    #endregion
}