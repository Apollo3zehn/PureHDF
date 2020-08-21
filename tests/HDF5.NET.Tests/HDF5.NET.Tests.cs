using HDF.PInvoke;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Xunit;

namespace HDF5.NET.Tests
{
    public class HDF5Tests
    {
        [Fact]
        public unsafe void Dummy()
        {
            // Arrange
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug();
            });

            var logger = loggerFactory.CreateLogger("");

            var filePath = Path.GetTempFileName();
            long res;

            var fileId = H5F.create(filePath, H5F.ACC_TRUNC);
            var groupId1 = H5G.create(fileId, "G1");
            var groupId2 = H5G.create(fileId, "G2");
            var groupId3 = H5G.create(groupId1, "G1.1");
            var dataspaceId = H5S.create_simple(1, new ulong[] { 1 }, new ulong[] { 1 });
            var datasetId = H5D.create(fileId, "D1", H5T.NATIVE_INT8, dataspaceId);

            var attributeDataspaceId1 = H5S.create_simple(1, new ulong[] { 2 }, new ulong[] { 2 });
            var attributeId1 = H5A.create(fileId, "A0", H5T.NATIVE_DOUBLE, attributeDataspaceId1);
            var attributeData1 = new double[] { 0.1, 0.2 };

            fixed (double* ptr = attributeData1)
            {
                res = H5A.write(attributeId1, H5T.NATIVE_DOUBLE, new IntPtr((void*)ptr));
            }

            var attributeDataspaceId2 = H5S.create_simple(1, new ulong[] { 2 }, new ulong[] { 4 });
            var attributeId2 = H5A.create(groupId3, "A1.1.1", H5T.NATIVE_DOUBLE, attributeDataspaceId1);
            var attributeData2 = new double[] { 2e-31, 99.98e+2 };

            fixed (double* ptr = attributeData2)
            {
                res = H5A.write(attributeId2, H5T.NATIVE_DOUBLE, new IntPtr((void*)ptr));
            }

            var attributeDataspaceId3 = H5S.create_simple(1, new ulong[] { 2 }, new ulong[] { 3 });
            var attributeId3 = H5A.create(groupId3, "A1.1.2", H5T.NATIVE_INT64, attributeDataspaceId1);
            var attributeData3 = new long[] { 0, 65561556, 2 };

            fixed (long* ptr = attributeData3)
            {
                res = H5A.write(attributeId3, H5T.NATIVE_INT64, new IntPtr((void*)ptr));
            }

            res = H5A.close(attributeId3);
            res = H5D.close(attributeDataspaceId3);

            res = H5A.close(attributeId2);
            res = H5D.close(attributeDataspaceId2);

            res = H5A.close(attributeId1);
            res = H5D.close(attributeDataspaceId1);

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