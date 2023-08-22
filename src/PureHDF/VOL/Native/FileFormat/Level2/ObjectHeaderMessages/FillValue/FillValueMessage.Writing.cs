// https://support.hdfgroup.org/HDF5/doc_resource/H5Fill_Behavior.html
namespace PureHDF.VOL.Native;

internal partial record class FillValueMessage
{
    public override ushort GetEncodeSize()
    {
        if (Version != 3)
            throw new Exception("Only version 3 fill value messages are supported.");

        var size =
            sizeof(byte) +
            sizeof(byte) +
            (
                Value is null
                    ? 0
                    : sizeof(uint) + Value.Length
            );
            
        return (ushort)size;
    }

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(Version);

        // flags
        byte flags = 0;

        flags |= (byte)((byte)AllocationTime & 0x03);
        flags |= (byte)(((byte)FillTime & 0x03) << 2);
        flags |= (byte)((Value is null ? 1 : 0) << 4);
        flags |= (byte)((Value is null ? 0 : 1) << 5);

        driver.Write(flags);

        if (Value is not null)
        {
            // size
            driver.Write((uint)Value.Length);

            // fill value
            driver.Write(Value);
        }
    }
}