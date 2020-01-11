using System;
using System.IO;

namespace HDF5.NET
{
    public abstract class DataLayoutMessage : Message
    {
        #region Constructors

        public DataLayoutMessage(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public LayoutClass LayoutClass { get; set; }

        #endregion

        #region Methods

        public static DataLayoutMessage Construct(BinaryReader reader, Superblock superblock)
        {
            // get version
            var version = reader.ReadByte();

            return version switch
            {
                1 => new DataLayoutMessage12(reader, superblock, version),
                2 => new DataLayoutMessage12(reader, superblock, version),
                3 => new DataLayoutMessage34(reader, superblock, version),
                4 => new DataLayoutMessage34(reader, superblock, version),
                _ => throw new NotSupportedException($"The data layout message version '{version}' is not supported.")
            };
        }

        #endregion
    }
}
