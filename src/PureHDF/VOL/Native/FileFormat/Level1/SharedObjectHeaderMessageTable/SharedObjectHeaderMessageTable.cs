using System.Text;

namespace PureHDF.VOL.Native;

// TODO: implement this correctly
// public List<byte> Versions { get; set; }
// public List<MessageTypeFlags> MessageTypeFlags { get; set; }
// public List<uint> MinimumMessageSize { get; set; }
// public List<ushort> ListCutoff { get; set; }
// public List<ushort> BTree2Cutoff { get; set; }
// public List<ushort> MessageCount { get; set; }
// public List<ulong> IndexAddress { get; set; }
// public List<ulong> FractalHeapAddress { get; set; }

internal readonly record struct SharedObjectHeaderMessageTable(
    //
)
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("SMTB");

    public static SharedObjectHeaderMessageTable Decode(H5DriverBase driver)
    {
        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, Signature);

        //
        // TODO: implement this correctly

        // checksum
        var _ = driver.ReadUInt32();

        return new SharedObjectHeaderMessageTable(
            //
        );
    }
}