using HDF.PInvoke;
using System;
using System.IO;
using Xunit;

namespace HDF5.NET.Tests
{
    public class HDF5Tests
    {
        [Fact]
        public void Dummy()
        {
            // Arrange
            var filePath = Path.GetTempFileName();
            var fileId = H5F.open(filePath, H5F.ACC_RDWR);
            var groupId = H5G.create(fileId, "Test");

            H5G.close(groupId);
            H5F.close(fileId);

            // Act
            var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            //h5file.OpenGroup("/Test");

            //// Assert
            //Assert.Throws<FormatException>(action);
        }
    }
}