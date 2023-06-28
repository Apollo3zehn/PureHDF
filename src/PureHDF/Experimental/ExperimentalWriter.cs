namespace PureHDF.Experimental;

internal static class H5Writer
{
    public static void Serialize(H5File file, string filePath)
    {
        using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var driver = new BinaryWriter(fileStream);

        // encode superblock
        var objectHeaderAddress = 
            (ulong)Superblock.FormatSignature.Length + 
            sizeof(byte) + sizeof(byte) + sizeof(byte) + sizeof(byte) + 
            sizeof(ulong) + sizeof(ulong) + sizeof(ulong) + sizeof(ulong) +
            sizeof(uint);

        var superblock = new Superblock23(
            Driver: default!,
            Version: 3,
            FileConsistencyFlags: default,
            BaseAddress: 0,
            ExtensionAddress: Superblock.UndefinedAddress,
            EndOfFileAddress: default, /* TODO write correct value */
            RootGroupObjectHeaderAddress: objectHeaderAddress,
            Checksum: default /* TODO write correct value */)
        {
            OffsetsSize = sizeof(ulong),
            LengthsSize = sizeof(ulong)
        };

        superblock.Encode(driver);

        // root group
        var objectHeader = new ObjectHeader2(
            Address: default,
            Flags: ObjectHeaderFlags.SizeOfChunk1 | ObjectHeaderFlags.SizeOfChunk2,
            AccessTime: default,
            ModificationTime: default,
            ChangeTime: default,
            BirthTime: default,
            MaximumCompactAttributesCount: default,
            MinimumDenseAttributesCount: default,
            SizeOfChunk0: default /* TODO write correct value */,
            HeaderMessages: new List<HeaderMessage>() /* TODO write correct value */
        )
        {
            Version = 2
        };

        objectHeader.Encode(driver);
    }
}