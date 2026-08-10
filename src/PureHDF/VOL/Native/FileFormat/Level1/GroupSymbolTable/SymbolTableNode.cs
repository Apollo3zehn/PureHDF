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

    public static async ValueTask<SymbolTableNode> Decode(NativeReadContext context)
    {
        var driver = context.Driver;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // reserved
        await driver.ReadByte().ConfigureAwait(false);

        // symbol count
        var symbolCount = await driver.ReadUInt16().ConfigureAwait(false);

        // group entries
        var groupEntries = new List<SymbolTableEntry>();

        for (int i = 0; i < symbolCount; i++)
        {
            groupEntries.Add(await SymbolTableEntry.Decode(context).ConfigureAwait(false));
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