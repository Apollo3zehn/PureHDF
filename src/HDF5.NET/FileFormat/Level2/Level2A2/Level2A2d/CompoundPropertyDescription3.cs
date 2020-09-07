using System;

namespace HDF5.NET
{
    public class CompoundPropertyDescription3 : CompoundPropertyDescription
    {
        #region Constructors

        public CompoundPropertyDescription3(H5BinaryReader reader, uint valueSize) : base(reader)
        {
            // name
            this.Name = H5Utils.ReadNullTerminatedString(reader, pad: false);

            // member byte offset
            var byteCount = H5Utils.FindMinByteCount(valueSize);

            if (!(1 <= byteCount && byteCount <= 8))
                throw new NotSupportedException("A compount property description member byte offset byte count must be within the range of 1..8.");

            var buffer = new byte[8];

            for (ulong i = 0; i < byteCount; i++)
            {
                buffer[i] = reader.ReadByte();
            }

            this.MemberByteOffset = BitConverter.ToUInt64(buffer);

            // member type message
            this.MemberTypeMessage = new DatatypeMessage(reader);
        }

        #endregion
    }
}