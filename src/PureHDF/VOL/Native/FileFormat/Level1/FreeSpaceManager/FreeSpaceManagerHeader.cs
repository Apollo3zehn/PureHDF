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

    public static async ValueTask<FreeSpaceManagerHeader> Decode(NativeReadContext context)
    {
        var (driver, superblock) = context;

        // signature
        var signature = await driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, FreeSpaceManagerHeader.Signature);

        // version
        var version = await driver.ReadByte().ConfigureAwait(false);

        // client ID
        var clientId = (ClientId)(await driver.ReadByte().ConfigureAwait(false));

        // total space tracked
        var totalSpaceTracked = await superblock.ReadLength(driver).ConfigureAwait(false);

        // total sections count
        var totalSectionsCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // serialized sections count
        var serializedSectionsCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // un-serialized sections count
        var unSerializedSectionsCount = await superblock.ReadLength(driver).ConfigureAwait(false);

        // section classes count
        var sectionClassesCount = await driver.ReadUInt16().ConfigureAwait(false);

        // shrink percent
        var shrinkPercent = await driver.ReadUInt16().ConfigureAwait(false);

        // expand percent
        var expandPercent = await driver.ReadUInt16().ConfigureAwait(false);

        // address space size
        var addressSpaceSize = await driver.ReadUInt16().ConfigureAwait(false);

        // maximum section size
        var maximumSectionSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // serialized section list address
        var serializedSectionListAddress = await superblock.ReadOffset(driver).ConfigureAwait(false);

        // serialized section list used
        var serializedSectionListUsed = await superblock.ReadLength(driver).ConfigureAwait(false);

        // serialized section list allocated size
        var serializedSectionListAllocatedSize = await superblock.ReadLength(driver).ConfigureAwait(false);

        // checksum
        var _ = await driver.ReadUInt32().ConfigureAwait(false);

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