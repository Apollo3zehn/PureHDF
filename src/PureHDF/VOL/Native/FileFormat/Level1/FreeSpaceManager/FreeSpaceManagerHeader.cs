using System.Text;

namespace PureHDF.VOL.Native;

// this should be a class because it has so many fields
internal record class FreeSpaceManagerHeader(
    ClientId ClientId,
    ulong TotalSpaceTracked,
    ulong TotalSectionsCount,
    ulong SerializedSectionsCount,
    ulong UnSerializedSectionsCount,
    ushort SectionClassesCount,
    ushort ShrinkPercent,
    ushort ExpandPercent,
    ushort AddressSpaceSize,
    ulong MaximumSectionSize,
    ulong SerializedSectionListAddress,
    ulong SerializedSectionListUsed,
    ulong SerializedSectionListAllocatedSize
)
{
    private byte _version;

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("FSHD");

    public required byte Version
    {
        get
        {
            return _version;
        }
        init
        {
            if (value != 0)
                throw new FormatException($"Only version 0 instances of type {nameof(FreeSpaceManagerHeader)} are supported.");

            _version = value;
        }
    }

    public static FreeSpaceManagerHeader Decode(NativeContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, FreeSpaceManagerHeader.Signature);

        // version
        var version = driver.ReadByte();

        // client ID
        var clientId = (ClientId)driver.ReadByte();

        // total space tracked
        var totalSpaceTracked = superblock.ReadLength(driver);

        // total sections count
        var totalSectionsCount = superblock.ReadLength(driver);

        // serialized sections count
        var serializedSectionsCount = superblock.ReadLength(driver);

        // un-serialized sections count
        var unSerializedSectionsCount = superblock.ReadLength(driver);

        // section classes count
        var sectionClassesCount = driver.ReadUInt16();

        // shrink percent
        var shrinkPercent = driver.ReadUInt16();

        // expand percent
        var expandPercent = driver.ReadUInt16();

        // address space size
        var addressSpaceSize = driver.ReadUInt16();

        // maximum section size
        var maximumSectionSize = superblock.ReadLength(driver);

        // serialized section list address
        var serializedSectionListAddress = superblock.ReadOffset(driver);

        // serialized section list used
        var serializedSectionListUsed = superblock.ReadLength(driver);

        // serialized section list allocated size
        var serializedSectionListAllocatedSize = superblock.ReadLength(driver);

        // checksum
        var _ = driver.ReadUInt32();

        return new FreeSpaceManagerHeader(
            ClientId: clientId,
            TotalSpaceTracked: totalSpaceTracked,
            TotalSectionsCount: totalSectionsCount,
            SerializedSectionsCount: serializedSectionsCount,
            UnSerializedSectionsCount: unSerializedSectionsCount,
            SectionClassesCount: sectionClassesCount,
            ShrinkPercent: shrinkPercent,
            ExpandPercent: expandPercent,
            AddressSpaceSize: addressSpaceSize,
            MaximumSectionSize: maximumSectionSize,
            SerializedSectionListAddress: serializedSectionListAddress,
            SerializedSectionListUsed: serializedSectionListUsed,
            SerializedSectionListAllocatedSize: serializedSectionListAllocatedSize
        )
        {
            Version = version
        };
    }
}