using System;

namespace HDF5.NET
{
    internal abstract class BTree2Node<T> : FileBlock where T : struct, IBTree2Record
    {
        #region Fields

        private byte _version;

        #endregion

        public BTree2Node(H5BinaryReader reader, BTree2Header<T> header, ushort recordCount, byte[] signature, Func<T> decodeKey) 
            : base(reader)
        {
            // signature
            var actualSignature = reader.ReadBytes(4);
            H5Utils.ValidateSignature(actualSignature, signature);

            // version
            Version = reader.ReadByte();

            // type
            Type = (BTree2Type)reader.ReadByte();

            if (Type != header.Type)
                throw new FormatException($"The BTree2 internal node type ('{Type}') does not match the type defined in the header ('{header.Type}').");

            // records
            Records = new T[recordCount];

            for (ulong i = 0; i < recordCount; i++)
            {
                Records[i] = decodeKey();
            }
        }

        #region Properties

        public byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                if (value != 0)
                    throw new FormatException($"Only version 0 instances of type {nameof(BTree2Node<T>)} are supported.");

                _version = value;
            }
        }

        public BTree2Type Type { get; }
        public T[] Records { get; }
        public uint Checksum { get; protected set; }

        #endregion
    }
}
