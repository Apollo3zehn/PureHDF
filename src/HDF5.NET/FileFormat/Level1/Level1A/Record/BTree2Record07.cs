using System;
using System.IO;

namespace HDF5.NET
{
    public abstract class BTree2Record07 : BTree2Record
    {
        #region Constructors

        public BTree2Record07(BinaryReader reader, MessageLocation messageLocation) : base(reader)
        {
            this.MessageLocation = messageLocation;
        }

        #endregion

        #region Properties

        public MessageLocation MessageLocation { get; }

        #endregion

        #region Properties

        public static BTree2Record07 Construct(BinaryReader reader, Superblock superblock)
        {
            var messageLocation = (MessageLocation)reader.ReadByte();

            return messageLocation switch
            {
                MessageLocation.Heap            => new BTree2Record07_0(reader, messageLocation),
                MessageLocation.ObjectHeader    => new BTree2Record07_1(reader, superblock, messageLocation),
                _                               => throw new Exception($"Unknown message location '{MessageLocation.Heap}'.")
            };
        }

        #endregion
    }
}
