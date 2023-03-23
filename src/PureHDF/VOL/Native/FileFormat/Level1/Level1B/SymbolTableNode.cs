using System.Text;

namespace PureHDF.VOL.Native;

internal class SymbolTableNode
{
    #region Fields

    private byte _version;

    #endregion

    #region Constructors

    public SymbolTableNode(NativeContext context)
    {
        var driver = context.Driver;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, SymbolTableNode.Signature);

        // version
        Version = driver.ReadByte();

        // reserved
        driver.ReadByte();

        // symbol count
        SymbolCount = driver.ReadUInt16();

        // group entries
        GroupEntries = new List<SymbolTableEntry>();

        for (int i = 0; i < SymbolCount; i++)
        {
            GroupEntries.Add(new SymbolTableEntry(context));
        }
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("SNOD");

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(SymbolTableNode)} are supported.");

            _version = value;
        }
    }

    public ushort SymbolCount { get; set; }
    public List<SymbolTableEntry> GroupEntries { get; set; }

    #endregion
}