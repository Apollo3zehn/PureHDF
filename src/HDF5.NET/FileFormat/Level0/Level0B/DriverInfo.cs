using System.IO;

namespace HDF5.NET
{
    public abstract class DriverInfo : FileBlock
    {
        public DriverInfo(BinaryReader reader) : base(reader)
        {
            //
        }
    }
}
