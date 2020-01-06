using HDF.PInvoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
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
            var serviceProvider = new ServiceCollection().AddLogging(builder =>
            {
                builder.AddDebug();
            }).BuildServiceProvider();

            var logger = serviceProvider.GetService<ILoggerFactory>().CreateLogger("");

            var filePath = Path.GetTempFileName();
            var fileId = H5F.open(filePath, H5F.ACC_RDWR);
            var groupId1 = H5G.create(fileId, "G1.1");
            var groupId2 = H5G.create(fileId, "G1.2");
            var groupId3 = H5G.create(groupId1, "G1.1.1");

            H5G.close(groupId3);
            H5G.close(groupId2);
            H5G.close(groupId1);
            H5F.close(fileId);

            // Act
            var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            h5file.Superblock.Print(logger);

            //h5file.OpenGroup("/Test");

            //// Assert
            //Assert.Throws<FormatException>(action);
        }
    }
}