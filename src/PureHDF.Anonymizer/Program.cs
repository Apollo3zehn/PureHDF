using PureHDF;
using PureHDF.VOL.Native;

var sourceFilePath = "/home/vincent/Dokumente/Git/GitHub/PureHDF/tests/PureHDF.Tests/TestFiles/lzf.h5";
// var sourceFilePath = default(string);

// while (!File.Exists(sourceFilePath))
// {
//     Console.WriteLine("Path of the HDF5 file to be anonymized (a copy will be created):");
//     sourceFilePath = Console.ReadLine();
// }

// Console.WriteLine();
// Console.WriteLine("Copy file ...");

var targetFilePath = Path.ChangeExtension(sourceFilePath, ".anonymized.h5");
var offsetsPath = Path.ChangeExtension(sourceFilePath, ".offsets");

try
{
    File.Copy(sourceFilePath, targetFilePath, overwrite: true);

    Console.WriteLine("Anonymize ...");

    if (File.Exists(offsetsPath))
        File.Delete(offsetsPath);

    using var targetFileStream = File.Open(targetFilePath, FileMode.Open, FileAccess.ReadWrite);
    using var root = (NativeFile)H5File.Open(targetFileStream);

    AnonymizeGroup(root);
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

static void AnonymizeGroup(NativeGroup group)
{
    Console.WriteLine($"Anonymize group {group.Name}");

    foreach (var child in group.Children().Cast<NativeObject>())
    {
        if (child is NativeGroup childGroup)
            AnonymizeGroup(childGroup);

        else if (child is NativeDataset dataset)
            AnonymizeDataset(dataset);

        AnonymizeLink(child);
    }

    AnonymizeAttributes(group);
}

static void AnonymizeAttributes(IH5AttributableObject @object)
{
    // throw new NotImplementedException();
}

static void AnonymizeDataset(NativeDataset dataset)
{
    Console.WriteLine($"Anonymize dataset {dataset.Name}");
    dataset.Read();
    AnonymizeAttributes(dataset);    
}

static void AnonymizeLink(NativeObject @object)
{
    Console.WriteLine($"Anonymize link {@object.Name}");
    // throw new NotImplementedException();
}