using HDF.PInvoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            var fileId = H5F.create(filePath, H5F.ACC_TRUNC);
            var groupId1 = H5G.create(fileId, "G1");
            var groupId2 = H5G.create(fileId, "G2");
            var groupId3 = H5G.create(groupId1, "G1.1");
            var dataspaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId = H5D.create(fileId, "D1", H5T.NATIVE_INT8, dataspaceId);

            long res;
            res = H5D.close(datasetId);
            res = H5S.close(dataspaceId);
            res = H5G.close(groupId3);
            res = H5G.close(groupId2);
            res = H5G.close(groupId1);
            res = H5F.close(fileId);

            // Act
            var h5file = H5File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            h5file.Superblock.Print(logger);

            var firstLevelChildren = h5file.Root.Children;

            //h5file.OpenGroup("/Test");

            //// Assert
            //Assert.Throws<FormatException>(action);
        }
    }
}