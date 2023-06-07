namespace PureHDF.VOL.Native;

internal static class AnonymizeHelper
{
    public static void Append(string? filePath, long offset, long length)
    {
        if (filePath is not null)
        {
            var offsetsFilePath = Path.ChangeExtension(filePath, ".offsets");

            File.AppendAllLines(offsetsFilePath, new string[] {
                $"{offset}, {length}"
            });
        }
    }
}