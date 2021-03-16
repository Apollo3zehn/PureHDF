using System;

namespace HDF5.NET
{
    internal abstract class DataLayoutMessage : Message
    {
        #region Constructors

        public DataLayoutMessage(H5BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public LayoutClass LayoutClass { get; set; }

        public ulong Address { get; set; }

        #endregion

        #region Methods

        public static DataLayoutMessage Construct(H5BinaryReader reader, Superblock superblock)
        {
            // get version
            var version = reader.ReadByte();

            return version switch
            {
                1 => new DataLayoutMessage12(reader, superblock, version),
                2 => new DataLayoutMessage12(reader, superblock, version),
                3 => new DataLayoutMessage3(reader, superblock, version),
                4 => new DataLayoutMessage4(reader, superblock, version),
                _ => throw new NotSupportedException($"The data layout message version '{version}' is not supported.")
            };
        }

        #endregion
    }
}
