using System.Buffers;
using PureHDF;
using PureHDF.VOL.Native;

var sourceFilePath = default(string);

while (!File.Exists(sourceFilePath))
{
    Console.WriteLine("Path of the HDF5 file to be anonymized (a copy will be created):");
    sourceFilePath = Console.ReadLine();
}

var targetFilePath = Path.ChangeExtension(sourceFilePath, ".anonymized.h5");

bool overwrite = false;

while (!overwrite)
{
    if (File.Exists(targetFilePath))
    {
        Console.WriteLine($"The file {targetFilePath} already exists. Overwrite? (y/N)");
        overwrite = Console.ReadLine() == "y";
    }

    else
    {
        break;
    }
}

var offsetsPath = Path.ChangeExtension(targetFilePath, ".offsets");

try
{
    Console.WriteLine();
    Console.WriteLine("Copy file ...");

    File.Copy(sourceFilePath, targetFilePath, overwrite: true);

    Console.WriteLine("Scanning ...");

    if (File.Exists(offsetsPath))
        File.Delete(offsetsPath);

    using (var root = H5File.OpenRead(targetFilePath))
    {
        ScanGroup(root);
    }

    Console.WriteLine("Anonymizing ...");
    var random = new Random();
    Anonymize(offsetsPath, targetFilePath, random);
}
catch
{
    try
    {
        if (File.Exists(targetFilePath))
            File.Delete(targetFilePath);
    }
    catch
    {
        //
    }

    try
    {
        if (File.Exists(offsetsPath))
            File.Delete(offsetsPath);
    }
    catch
    {
        //
    }

    throw;
}

Console.WriteLine($"The anonymized file has been created at {targetFilePath}");

static void Anonymize(string offsetsPath, string targetFilePath, Random random)
{
    var processed = new HashSet<AnonymizeInfo>();

    using var offsetsReader = new StreamReader(offsetsPath);
    using var targetWriter = new BinaryWriter(File.OpenWrite(targetFilePath));
    string? line;

    var baseAddress = long.Parse(offsetsReader
        .ReadLine()!
        .Split(',')[1]);

    while ((line = offsetsReader.ReadLine()) != null)
    {
        var parts = line.Split(',');
        var category = parts[0];
        var offset = long.Parse(parts[1]);
        var length = long.Parse(parts[2]);
        var addBaseAddress = parts[3] == "True";
        var info = new AnonymizeInfo(offset, length);
        var isNew = processed.Add(info);

        if (isNew)
        {
            Console.WriteLine($"Anonymize category {category} @ offset {offset} and length {length}");

            var actualBaseAddress = addBaseAddress
                ? baseAddress
                : 0;

            targetWriter.BaseStream.Seek(actualBaseAddress + offset, SeekOrigin.Begin);

            /* Renaming links and attributes causes hashes to become invalid.
             * This might be a problem when links/attributes are accessed directly,
             * instead of enumerating them. Maybe this explains why HDF View sometimes
             * fails to recognize the object types after running the anonymizer.
             */
            if (category == "local-heap")
            {
                var randomString = RandomStringAsCharArray((int)length, random);
                targetWriter.Write(randomString);
            }

            else if (category == "attribute-name")
            {
                var randomString = RandomStringAsCharArray((int)(length - 1), random);
                targetWriter.Write(randomString);
            }

            else
            {
                using var owner = MemoryPool<byte>.Shared.Rent((int)length);
                owner.Memory.Span.Clear();

                var span = owner.Memory.Span[..(int)length];
                targetWriter.Write(span);
            }
        }
    }
}

static void ScanGroup(INativeGroup group)
{
    Console.WriteLine($"Scan group {group.Name}");

    foreach (var child in group.Children().Cast<NativeObject>())
    {
        if (child is INativeGroup childGroup)
            ScanGroup(childGroup);

        else if (child is INativeDataset dataset)
            ScanDataset(dataset);
    }

    ScanAttributes(group);
}

static void ScanAttributes(IH5AttributableObject @object)
{
    var _ = @object
        .Attributes()
        .ToList();
}

static void ScanDataset(INativeDataset dataset)
{
    Console.WriteLine($"Scan dataset {dataset.Name}");
    dataset.Read();

    ScanAttributes(dataset);    
}

static char[] RandomStringAsCharArray(int length, Random random)
{
    const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    return Enumerable.Repeat(chars, length)
        .Select(value => value[random.Next(value.Length)])
        .ToArray();
}

record AnonymizeInfo(long offset, long length);