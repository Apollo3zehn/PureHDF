using Xunit;

namespace PureHDF.Tests.Reading;

public class FilePathUtilsTests
{
    [Theory]
    public void CanFindFileExternalFileForLinkAccess()
    {
        var thisFolderPath = EnsureThisFolder();
        var filePath = EnsureFile();

        var linkAccess = new H5LinkAccess(
            ExternalLinkPrefix: prefix
        );

        FilePathUtils.FindExternalFileForLinkAccess(thisFolderPath, filePath, linkAccess);
    }

    [Theory]
    public void CanFindFileExternalFileForDatasetAccess()
    {

    }

    [Theory]
    public void CanFindVirtualFile()
    {

    }

    private static string EnsureThisFolder()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempFolder);

        return tempFolder;
    }

    private static string EnsureFile()
    {
        var filePath = Path.GetRandomFileName();
        using (File.Create(filePath)) {}
        return filePath;
    }
}