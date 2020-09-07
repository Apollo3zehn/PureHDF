using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class ObjectHeaderContinuationBlock2 : ObjectHeader
    {
        #region Constructors

        public ObjectHeaderContinuationBlock2(BinaryReader reader, Superblock superblock, ulong objectHeaderSize, byte version, bool withCreationOrder) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, ObjectHeaderContinuationBlock2.Signature);

            // header messages
            var messages = this.ReadHeaderMessages(reader, superblock, objectHeaderSize - 8, version, withCreationOrder);
            this.HeaderMessages.AddRange(messages);

#warning H5OCache.c (L. 1595)  /* Gaps should only occur in chunks with no null messages */
#warning read gap and checksum
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OCHK");

        public uint Checksum { get; set; }

        #endregion
    }
}
