using Xunit;

namespace PureHDF.Tests.Writing;

public class LinkTests
{
    [Fact]
    public void CanWrite_SoftLinks()
    {
        // Arrange
        var file = new H5File
        {
            ["group_1"] = new H5Group
            {
                ["dataset"] = new int[] { 1, 2, 3 }
            },

            ["group_2"] = new H5Group
            {
                ["soft_link_1"] = new H5SoftLink("/group_1"),
                ["soft_link_2"] = new H5SoftLink("/group_1/dataset")
            }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath);

            // Assert
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/links_soft.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_Mass()
    {
        // Arrange
        var file = new H5File();

        for (int i = 0; i < 1000; i++)
        {
            file[i.ToString()] = new H5Group();
        }

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath);

            // Assert
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/links_mass.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_SharedDataset()
    {
        // A single H5Dataset instance attached at multiple locations must be
        // encoded once and referenced by hard links (HDF5 hard-link semantics).

        // Arrange
        var shared = new H5Dataset(data: new int[] { 1, 2, 3 });

        var file = new H5File
        {
            ["dataset_a"] = shared,

            ["group"] = new H5Group
            {
                ["dataset_b"] = shared
            }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath);

            // Assert
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/links_shared_dataset.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void SharedDataset_RoundTrips()
    {
        // Both hard links must resolve to the same data when read back.

        // Arrange
        var expected = new int[] { 1, 2, 3 };
        var shared = new H5Dataset(data: expected);

        var file = new H5File
        {
            ["dataset_a"] = shared,

            ["group"] = new H5Group
            {
                ["dataset_b"] = shared
            }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath);

            // Assert
            using var root = H5File.OpenRead(filePath);

            var actualA = root.Dataset("dataset_a").Read<int[]>();
            var actualB = root.Group("group").Dataset("dataset_b").Read<int[]>();

            Assert.Equal(expected, actualA);
            Assert.Equal(expected, actualB);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_SharedRawValue()
    {
        // A raw value (not wrapped in H5Dataset) assigned to multiple locations must
        // also be encoded once and referenced by hard links.

        // Arrange
        var shared = new int[] { 1, 2, 3 };

        var file = new H5File
        {
            ["dataset_a"] = shared,

            ["group"] = new H5Group
            {
                ["dataset_b"] = shared
            }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath);

            // Assert
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/links_shared_dataset.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void SharedRawValue_RoundTrips()
    {
        // Both hard links to a shared raw value must resolve to the same data.

        // Arrange
        var expected = new int[] { 1, 2, 3 };

        var file = new H5File
        {
            ["dataset_a"] = expected,

            ["group"] = new H5Group
            {
                ["dataset_b"] = expected
            }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath);

            // Assert
            using var root = H5File.OpenRead(filePath);

            var actualA = root.Dataset("dataset_a").Read<int[]>();
            var actualB = root.Group("group").Dataset("dataset_b").Read<int[]>();

            Assert.Equal(expected, actualA);
            Assert.Equal(expected, actualB);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void CanWrite_Complex()
    {
        // Arrange
        var group_0 = new H5Group
        {
            ["dset"] = new H5Dataset(data: new float[] { 1.1f, 2.2f })
            {
                Attributes = new()
                {
                    ["attr"] = new int[] { -2, -3, -4 }
                }
            },

            Attributes = new()
            {
                ["attr"] = new double[] { 2.0, 3.1, 4.2 }
            }
        };

        var file = new H5File
        {
            ["group_0"] = group_0,

            ["group_1"] = new H5Group
            {
                ["group_0"] = group_0
            },

            ["dataset_0"] = new float[] { 1.1f },

            Attributes = new()
            {
                ["fixed_point_unsigned"] = new uint[] { 1, 2, 3 },
                ["fixed_point_signed"] = new int[] { 1, -2, 3 },
                ["floating_point_32"] = new float[] { 1.1f, -2.2e36f, 3.3f },
                ["floating_point_64"] = new double[] { 1.1, -2.2e36, 3.3 }
            }
        };

        var filePath = Path.GetTempFileName();

        try
        {
            // Act
            file.Write(filePath);

            // Assert
            var actual = TestUtils.DumpH5File(filePath);

            var expected = File
                .ReadAllText("DumpFiles/links_complex.dump")
                .Replace("<file-path>", filePath);

            Assert.Equal(expected, actual);
        }

        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    /// <summary>
    /// A link name may not contain '/', which HDF5 reserves as the path separator.
    /// </summary>
    /// <remarks>
    /// Nothing rejected it, and the result was a file no HDF5 C library tool can traverse: h5dump exits
    /// with an error stack, h5ls reports nothing below the root group, and h5py lists the key but cannot
    /// open it. The dictionary-shaped API makes <c>file["group/child"]</c> an easy thing to write.
    /// <para>
    /// Only LINK names are affected. An attribute name is not a path component, and the C library does
    /// allow a slash there - h5dump prints such an attribute without complaint - so attribute names are
    /// deliberately left alone.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("group/child")]
    [InlineData("/leading")]
    [InlineData("trailing/")]
    public void ALinkNameMayNotContainASlash(string name)
    {
        // Arrange
        var file = new H5File { [name] = new H5Dataset(new int[] { 1, 2, 3 }) };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => file.Write(new MemoryStream()));

        // Assert - the message has to name the character and the link, since the call site that created
        // it is a dictionary assignment with no stack of its own.
        Assert.Contains(name, exception.Message);
        Assert.Contains("'/'", exception.Message);
    }

    /// <summary>An attribute name is not a path, so a slash in one stays legal.</summary>
    [Fact]
    public void AnAttributeNameMayContainASlash()
    {
        // Arrange
        var file = new H5File();
        file.Attributes["has/slash"] = 42;

        // Act
        var stream = new MemoryStream();
        file.Write(stream);

        // Assert
        stream.Seek(0, SeekOrigin.Begin);

        using var actual = H5File.Open(stream);

        Assert.Equal(42, actual.Attribute("has/slash").Read<int>());
    }

}