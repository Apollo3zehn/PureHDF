﻿using HDF.PInvoke;
using Xunit;

namespace PureHDF.Tests.Reading;

public partial class DatasetTests
{
    [Fact]
    public void CanRead_Contiguous()
    {
        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddContiguousDataset(fileId));

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var parent = root.Group("contiguous");
            var dataset = parent.Dataset("contiguous");
            var actual = dataset.Read<int[]>();

            // Assert
            Assert.True(actual.SequenceEqual(SharedTestData.HugeData));
        });
    }

    // https://support.hdfgroup.org/HDF5/doc_resource/H5Fill_Behavior.html
    // Fill value can only be inserted during read when data space is not allocated (late allocation).
    // As soon as the allocation happened, the fill value is either written or not written but during 
    // read this cannot be distinguished anymore. It is not possible to determine which parts of the
    // dataset have not been touched to insert a fill value in these buffers.
    [Fact]
    public void CanRead_Contiguous_With_FillValue_And_AllocationLate()
    {
        // Arrange
        var version = H5F.libver_t.LATEST;
        var fillValue = 99;
        var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddContiguousDatasetWithFillValueAndAllocationLate(fileId, fillValue));
        var expected = Enumerable.Range(0, SharedTestData.MediumData.Length)
            .Select(value => fillValue)
            .ToArray();

        // Act
        using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
        var group = root.Group("fillvalue");
        var dataset = group.Dataset($"{LayoutClass.Contiguous}");
        var actual = dataset.Read<int[]>();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanRead_External()
    {
        // INFO:
        // HDF lib says "external storage not supported with chunked layout". Same is true for compact layout.

        TestUtils.RunForAllVersions(version =>
        {
            // Arrange
            var filePath = TestUtils.PrepareTestFile(version, fileId => TestUtils.AddExternalDataset(fileId, "external_file"));
            var expected = SharedTestData.MediumData.ToArray();

            for (int i = 33; i < 40; i++)
            {
                expected[i] = 0;
            }

            // Act
            using var root = NativeFile.InternalOpenRead(filePath, deleteOnClose: true);
            var parent = root.Group("external");
            var dataset = parent.Dataset("external_file");
            var actual = dataset.Read<int[]>();

            // Assert
            Assert.True(actual.SequenceEqual(expected));
        });
    }
}