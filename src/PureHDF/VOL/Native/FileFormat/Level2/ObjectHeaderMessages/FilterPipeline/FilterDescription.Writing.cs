using System.Runtime.InteropServices;
using System.Text;

namespace PureHDF.VOL.Native;

internal readonly partial record struct FilterDescription
{
    public ushort GetEncodeSize()
    {
        var isUnknown = (ushort)Identifier >= ushort.MaxValue;

        var size =
            sizeof(ushort) +
            (isUnknown ? 2 : 0) +
            sizeof(ushort) +
            sizeof(ushort) +
            (isUnknown ? Name.Length + 1 : 0) +
            sizeof(uint) * ClientData.Length;
            
        return (ushort)size;
    }

    public void Encode(H5DriverBase driver)
    {
        var identifierBytes = default(byte[]);
        var isUnknown = (ushort)Identifier >= ushort.MaxValue;

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
        {
            driver.Write(identifierBytes);
            driver.Write(0);
        }

        // client data
        driver.Write(MemoryMarshal.AsBytes<uint>(ClientData));
    }
}