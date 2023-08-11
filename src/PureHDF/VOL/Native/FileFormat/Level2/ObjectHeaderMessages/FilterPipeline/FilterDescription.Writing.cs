using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF.VOL.Native;

internal readonly partial record struct FilterDescription
{
    public ushort GetEncodeSize()
    {
        var isUnknown = Identifier >= byte.MaxValue;

        var size =
            sizeof(ushort) +
            (isUnknown ? 2 : 0) +
            sizeof(ushort) +
            sizeof(ushort) +
            (isUnknown ? Name.Length : 0) +
            sizeof(uint) * ClientData.Length;
            
        return (ushort)size;
    }

    public void Encode(H5DriverBase driver)
    {
        var identifierBytes = default(byte[]);
        var isUnknown = Identifier >= byte.MaxValue;

        // filter identification value
        driver.Write(Identifier);

        // name length
        if (isUnknown)
        {
            identifierBytes = Encoding.ASCII.GetBytes(Name);
            driver.Write((ushort)identifierBytes.Count());
        }

        // flags
        driver.Write(Flags);

        // number client data values
        driver.Write((ushort)ClientData.Length);

        // name
        if (identifierBytes is not null)
            driver.Write(identifierBytes);

        // client data
        driver.Write(MemoryMarshal.AsBytes<uint>(ClientData));
    }
}