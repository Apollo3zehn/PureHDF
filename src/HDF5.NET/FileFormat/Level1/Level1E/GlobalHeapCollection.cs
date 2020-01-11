using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public class GlobalHeapCollection : FileBlock
    {
        #region Fields

        private byte _version;
        private ulong _collectionSize;

        #endregion

        #region Constructors

        public GlobalHeapCollection(BinaryReader reader) : base(reader)
        {
            // signature
            var signature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(signature, BTree2LeafNode.Signature);

            // version
            this.Version = reader.ReadByte();

            // collection size
            this.Version = reader.ReadByte();

            // global heap objects
            this.GlobalHeapObjects = new List<GlobalHeapObject>();

#warning Finish implementation.
            //for (int i = 0; i < length; i++)
            //{

            //}
        }

        #endregion

        #region Properties

        public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("GCOL");

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 1)
                    throw new FormatException($"Only version 1 instances of type {nameof(GlobalHeapCollection)} are supported.");

                _version = value;
            }
        }

        public ulong CollectionSize
        {
            get
            {
                return _collectionSize;
            }
            set
            {
                if (value < 4096)
                    throw new FormatException("The minimum global heap collection size is 4096 bytes.");

                _collectionSize = value;
            }
        }

        public List<GlobalHeapObject> GlobalHeapObjects { get; set; }

        #endregion
    }
}
