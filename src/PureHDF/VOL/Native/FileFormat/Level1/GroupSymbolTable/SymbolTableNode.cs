using System.Text;

namespace PureHDF.VOL.Native;

internal readonly record struct SymbolTableNode(
    ushort SymbolCount,
    List<SymbolTableEntry> GroupEntries
)
{
    private readonly byte _version;

    public static byte[] Signature { get; set; } = Encoding.ASCII.GetBytes("SNOD");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 1)
                throw new FormatException($"Only version 1 instances of type {nameof(SymbolTableNode)} are supported.");

            _version = value;
        }
    }

    public static SymbolTableNode Decode(NativeContext context)
    {
        var driver = context.Driver;

        // signature
        var signature = driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // version
        var version = driver.ReadByte();

        // reserved
        driver.ReadByte();

        // symbol count
        var symbolCount = driver.ReadUInt16();

        // group entries
        var groupEntries = new List<SymbolTableEntry>();

        for (int i = 0; i < symbolCount; i++)
        {
            groupEntries.Add(SymbolTableEntry.Decode(context));
        }

        return new SymbolTableNode(
            SymbolCount: symbolCount,
            GroupEntries: groupEntries
        )
        {
            Version = version
        };
    }
}