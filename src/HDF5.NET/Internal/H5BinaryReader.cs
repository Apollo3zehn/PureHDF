using System;
using System.IO;

namespace HDF5.NET
{
    public class H5BinaryReader : BinaryReader
    {
        #region Constructors

        public H5BinaryReader(Stream input) : base(input)
        {
            //
        }

        #endregion

        #region Properties

        public ulong BaseAddress { get; set; }

        #endregion

        #region Methods

        public void Seek(long offset, SeekOrigin seekOrigin)
        {
            switch (seekOrigin)
            {
                case SeekOrigin.Begin:
                    this.BaseStream.Seek((long)this.BaseAddress + offset, seekOrigin); break;

                case SeekOrigin.Current:
                case SeekOrigin.End:
                    this.BaseStream.Seek(offset, seekOrigin); break;

                default:
                    throw new Exception($"Seek origin '{seekOrigin}' is not supported.");
            }
        }

        #endregion
    }
}
