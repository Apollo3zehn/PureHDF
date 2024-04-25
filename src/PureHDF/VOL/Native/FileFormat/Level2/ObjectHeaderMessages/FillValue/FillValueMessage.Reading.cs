// https://support.hdfgroup.org/HDF5/doc_resource/H5Fill_Behavior.html
namespace PureHDF.VOL.Native;

internal partial record class FillValueMessage(
    SpaceAllocationTime AllocationTime,
    FillValueWriteTime FillTime,
    byte[]? Value
) : Message
{
    private byte _version;

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (!(1 <= value && value <= 3))
                throw new FormatException($"Only version 1-3 instances of type {nameof(FillValueMessage)} are supported.");

            _version = value;
        }
    }

    public static FillValueMessage Decode(H5DriverBase driver)
    {
        // see also H5dcpl.c (H5P_is_fill_value_defined) and H5Dint.c (H5D__update_oh_info):
        // if size = 0 then default value should be applied
        // if size = -1 then fill value is explicitly undefined

        // version
        var version = driver.ReadByte();

        uint size;
        SpaceAllocationTime allocationTime;
        FillValueWriteTime fillTime;

        var value = default(byte[]);

        switch (version)
        {
            case 1:

                allocationTime = (SpaceAllocationTime)driver.ReadByte();
                fillTime = (FillValueWriteTime)driver.ReadByte();

                var isDefined1 = driver.ReadByte() == 1;

                if (isDefined1)
                {
                    size = driver.ReadUInt32();
                    value = driver.ReadBytes((int)size);
                }

                break;

            case 2:

                allocationTime = (SpaceAllocationTime)driver.ReadByte();
                fillTime = (FillValueWriteTime)driver.ReadByte();
                var isDefined2 = driver.ReadByte() == 1;

                if (isDefined2)
                {
                    size = driver.ReadUInt32();
                    value = driver.ReadBytes((int)size);
                }

                break;

            case 3:

                var flags = driver.ReadByte();
                allocationTime = (SpaceAllocationTime)((flags & 0x03) >> 0);    // take only bits 0 and 1
                fillTime = (FillValueWriteTime)((flags & 0x0C) >> 2);           // take only bits 2 and 3
                var isUndefined = (flags & (1 << 4)) > 0;                       // take only bit 4
                var isDefined3 = (flags & (1 << 5)) > 0;                        // take only bit 5

                // undefined
                if (isUndefined)
                {
                    value = null;
                }

                // defined
                else if (isDefined3)
                {
                    size = driver.ReadUInt32();
                    value = driver.ReadBytes((int)size);
                }

                break;

            default:
                throw new Exception("Unsupported version");
        }

        return new FillValueMessage(
            AllocationTime: allocationTime,
            FillTime: fillTime,
            Value: value?.Length > 0
                ? value
                : default
        )
        {
            Version = version
        };
    }

    public static FillValueMessage Decode(SpaceAllocationTime allocationTime)
    {
        return new FillValueMessage(
            AllocationTime: allocationTime,
            FillTime: FillValueWriteTime.IfSetByUser,
            Value: default
        )
        {
            Version = 3
        };
    }
}