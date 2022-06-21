using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HDF5.NET
{
    internal class H5D_Compact : H5D_Base
    {
        #region Constructors

        public H5D_Compact(H5Dataset dataset, H5DatasetAccess datasetAccess) : 
            base(dataset, supportsBuffer: true, supportsStream: false, datasetAccess)
        {
            //
        }

        #endregion

        #region Properties

        #endregion

        #region Methods

        public override ulong[] GetChunkDims()
        {
            return Dataset.InternalDataspace.DimensionSizes;
        }

        public override Memory<byte> GetBuffer(ulong[] chunkIndices)
        {
            byte[] buffer;

            if (Dataset.InternalDataLayout is DataLayoutMessage12 layout12)
            {
#warning untested
                buffer = layout12.CompactData;
            }
            else if (Dataset.InternalDataLayout is DataLayoutMessage3 layout34)
            {
                var compact = (CompactStoragePropertyDescription)layout34.Properties;
                buffer = compact.RawData;
            }
            else
            {
                throw new Exception($"Data layout message type '{Dataset.InternalDataLayout.GetType().Name}' is not supported.");
            }

            return buffer;
        }

        public override Stream? GetStream(ulong[] chunkIndices)
        {
            throw new NotImplementedException();
        }

#warning Use this instead of SpanExtensions!
        // https://docs.microsoft.com/en-us/dotnet/api/system.array?view=netcore-3.1
        // max array length is 0X7FEFFFFF = int.MaxValue - 1024^2 bytes
        // max multi dim array length seems to be 0X7FEFFFFF x 2, but no confirmation found
        private unsafe T ReadCompactMultiDim<T>()
        {
            // vllt. einfach eine zweite Read<T> Methode (z.B. ReadMultiDim), 
            // die keine generic constraint hat (leider), aber T zuerst auf IsArray
            // geprüft wird
            // beide Methoden definieren dann ein Lambda, um den Buffer entsprechender
            // Größe zu erzeugen. Dieser Buffer wird dann gefüllt und kann von der 
            // jeweiligen Methode mit dem korrekten Typ zurückgegeben werden

            //var a = ReadCompactMultiDim<T[,,]>();
            var type = typeof(T);

            var lengths = new int[] { 100, 200, 10 };
            var size = lengths.Aggregate(1L, (x, y) => x * y);
            object[] args = lengths.Cast<object>().ToArray();

            var buffer = (T)Activator.CreateInstance(type, args);

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var span = new Span<byte>(handle.AddrOfPinnedObject().ToPointer(), (int)size);
                span.Fill(0x25);
                return buffer;
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion
    }
}
