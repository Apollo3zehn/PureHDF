namespace PureHDF.VOL.Native;

internal static class AnonymizeHelper
{
    public static void Append(
        string category, 
        string? filePath, 
        long offset, 
        long length, 
        bool addBaseAddress)
    {
        if (filePath is not null)
        {
            var offsetsFilePath = Path.ChangeExtension(filePath, ".offsets");

            File.AppendAllLines(offsetsFilePath, new string[] {
                $"{category},{offset},{length},{addBaseAddress}"
            });
        }
    }
}