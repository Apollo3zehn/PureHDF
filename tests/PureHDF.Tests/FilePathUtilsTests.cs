using Xunit;

namespace PureHDF.Tests.Reading;

public class FilePathUtilsTests
{
    private const string THIS_FOLDER_PATH = "/this/folder/path";

    #region FindExternalFileForLinkAccess

    [Fact]
    public void CanFindExternalFileForLinkAccess_absolute()
    {
        // Arrange
        var expected = "/absolute/file/path/file.h5";

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForLinkAccess(
                default, expected, default, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForLinkAccess_relative_env()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var envPrefix = "/env/variable:/env/variable2";
        var expected = Path.Combine(envPrefix.Split(':')[1], relativePath);

        bool fileExists(string filePath) => filePath == expected;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXT_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindExternalFileForLinkAccess(
                    default, relativePath, default, fileExists);

            // Assert
            Assert.Equal(expected, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXT_PREFIX, default);
        }
    }

    [Fact]
    public void CanFindExternalFileForLinkAccess_relative_link_access()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var linkAccessPrefix = "/link/access";
        var expected = Path.Combine(linkAccessPrefix, relativePath);

        var linkAccess = new H5LinkAccess(ExternalLinkPrefix: linkAccessPrefix);

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForLinkAccess(
                default, relativePath, linkAccess, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForLinkAccess_relative_this_folder_path()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var expected = Path.Combine(THIS_FOLDER_PATH, relativePath);

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForLinkAccess(
                THIS_FOLDER_PATH, relativePath, default, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForLinkAccess_relative()
    {
        // Arrange
        var expected = "path/file.h5";

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForLinkAccess(
                default, expected, default, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForLinkAccess_relative_everything()
    {
        // Arrange
        var expected = "path/file.h5";
        var envPrefix = "/env/variable:/env/variable2";
        var linkAccessPrefix = "/link/access";

        var linkAccess = new H5LinkAccess(ExternalLinkPrefix: linkAccessPrefix);

        bool fileExists(string filePath) => filePath == expected;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXT_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindExternalFileForLinkAccess(
                    THIS_FOLDER_PATH, expected, linkAccess, fileExists);

            // Assert
            Assert.Equal(expected, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXT_PREFIX, default);
        }
    }

    [Fact]
    public void CanFindExternalFileForLinkAccess_fail()
    {
        // Arrange
        var filePath = "path/file.h5";
        var envPrefix = "/env/variable:/env/variable2";
        var linkAccessPrefix = "/link/access";

        var linkAccess = new H5LinkAccess(ExternalLinkPrefix: linkAccessPrefix);

        static bool fileExists(string filePath) => false;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXT_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindExternalFileForLinkAccess(
                    THIS_FOLDER_PATH, filePath, linkAccess, fileExists);

            // Assert
            Assert.Null(actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXT_PREFIX, default);
        }
    }

    #endregion

    #region FindVirtualFile

    [Fact]
    public void CanFindVirtualFile_self()
    {
        // Arrange
        var expected = ".";

        // Act
        var actual = FilePathUtils
            .FindVirtualFile(
                default, expected, default);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindVirtualFile_relative_env()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var envPrefix = "/env/variable:/env/variable2";
        var expected = Path.Combine(envPrefix.Split(':')[1], relativePath);

        bool fileExists(string filePath) => filePath == expected;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_VDS_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindVirtualFile(
                    default, relativePath, default, fileExists);

            // Assert
            Assert.Equal(expected, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_VDS_PREFIX, default);
        }
    }

    [Fact]
    public void CanFindVirtualFile_relative_dataset_access()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var datasetAccessPrefix = "/dataset/access";
        var expected = Path.Combine(datasetAccessPrefix, relativePath);

        var datasetAccess = new H5DatasetAccess(ExternalFilePrefix: datasetAccessPrefix);

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindVirtualFile(
                default, relativePath, datasetAccess, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindVirtualFile_relative_this_folder_path()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var expected = Path.Combine(THIS_FOLDER_PATH, relativePath);

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindVirtualFile(
                THIS_FOLDER_PATH, relativePath, default, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindVirtualFile_relative()
    {
        // Arrange
        var expected = "path/file.h5";

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindVirtualFile(
                default, expected, default, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindVirtualFile_relative_everything()
    {
        // Arrange
        var expected = "path/file.h5";
        var envPrefix = "/env/variable:/env/variable2";
        var datasetAccessPrefix = "/dataset/access";

        var datasetAccess = new H5DatasetAccess(ExternalFilePrefix: datasetAccessPrefix);

        bool fileExists(string filePath) => filePath == expected;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_VDS_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindVirtualFile(
                    THIS_FOLDER_PATH, expected, datasetAccess, fileExists);

            // Assert
            Assert.Equal(expected, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_VDS_PREFIX, default);
        }
    }

    [Fact]
    public void CanFindVirtualFile_fail()
    {
        // Arrange
        var filePath = "path/file.h5";
        var envPrefix = "/env/variable:/env/variable2";
        var datasetAccessPrefix = "/dataset/access";

        var datasetAccess = new H5DatasetAccess(ExternalFilePrefix: datasetAccessPrefix);

        static bool fileExists(string filePath) => false;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_VDS_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindVirtualFile(
                    THIS_FOLDER_PATH, filePath, datasetAccess, fileExists);

            // Assert
            Assert.Null(actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_VDS_PREFIX, default);
        }
    }

    #endregion

    #region FindExternalFileForDatasetAccess

    [Fact]
    public void CanFindExternalFileForDatasetAccess_absolute()
    {
        // Arrange
        var expected = "/absolute/file/path/file.h5";

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForDatasetAccess(
                default, expected, default, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForDatasetAccess_relative_env()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var envPrefix = "/env/variable";
        var expected = Path.Combine(envPrefix, relativePath);

        bool fileExists(string filePath) => filePath == expected;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, envPrefix);

        try
        {

            // Act
            var actual = FilePathUtils
                .FindExternalFileForDatasetAccess(
                    default, relativePath, default, fileExists);

            // Assert
            Assert.Equal(expected, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, default);
        }
    }

    [Fact]
    public void CanFindExternalFileForDatasetAccess_relative_env_origin()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var envPrefix = $"{FilePathUtils.ORIGIN_TOKEN}/env/variable";

        var expected = Path.Combine(
            THIS_FOLDER_PATH, 
            envPrefix.Replace(FilePathUtils.ORIGIN_TOKEN, ""), 
            relativePath);

        bool fileExists(string filePath) => filePath == expected;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindExternalFileForDatasetAccess(
                    THIS_FOLDER_PATH, relativePath, default, fileExists);

            // Assert
            Assert.Equal(expected, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, default);
        }
    }

    [Fact]
    public void CanFindExternalFileForDatasetAccess_relative_dataset_access()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var datasetAccessPrefix = "/dataset/access";
        var expected = Path.Combine(datasetAccessPrefix, relativePath);

        var datasetAccess = new H5DatasetAccess(ExternalFilePrefix: datasetAccessPrefix);

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForDatasetAccess(
                default, relativePath, datasetAccess, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForDatasetAccess_relative_dataset_access_origin()
    {
        // Arrange
        var relativePath = "path/file.h5";
        var datasetAccessPrefix = $"{FilePathUtils.ORIGIN_TOKEN}/dataset/access";

        var expected = Path.Combine(
            THIS_FOLDER_PATH, 
            datasetAccessPrefix.Replace(FilePathUtils.ORIGIN_TOKEN, ""), 
            relativePath);

        var datasetAccess = new H5DatasetAccess(ExternalFilePrefix: datasetAccessPrefix);

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForDatasetAccess(
                THIS_FOLDER_PATH, relativePath, datasetAccess, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForDatasetAccess_relative()
    {
        // Arrange
        var expected = "path/file.h5";

        bool fileExists(string filePath) => filePath == expected;

        // Act
        var actual = FilePathUtils
            .FindExternalFileForDatasetAccess(
                default, expected, default, fileExists);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanFindExternalFileForDatasetAccess_relative_everything()
    {
        // Arrange
        var expected = "path/file.h5";
        var envPrefix = "/env/variable";
        var datasetAccessPrefix = "/dataset/access";

        var datasetAccess = new H5DatasetAccess(ExternalFilePrefix: datasetAccessPrefix);

        bool fileExists(string filePath) => filePath == expected;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindExternalFileForDatasetAccess(
                    THIS_FOLDER_PATH, expected, datasetAccess, fileExists);

            // Assert
            Assert.Equal(expected, actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, default);
        }
    }

    [Fact]
    public void CanFindExternalFileForDatasetAccess_fail()
    {
        // Arrange
        var filePath = "path/file.h5";
        var envPrefix = "/env/variable";
        var datasetAccessPrefix = "/dataset/access";

        var datasetAccess = new H5DatasetAccess(ExternalFilePrefix: datasetAccessPrefix);

        static bool fileExists(string filePath) => false;

        Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, envPrefix);

        try
        {
            // Act
            var actual = FilePathUtils
                .FindExternalFileForDatasetAccess(
                    THIS_FOLDER_PATH, filePath, datasetAccess, fileExists);

            // Assert
            Assert.Null(actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FilePathUtils.HDF5_EXTFILE_PREFIX, default);
        }
    }

    #endregion
}