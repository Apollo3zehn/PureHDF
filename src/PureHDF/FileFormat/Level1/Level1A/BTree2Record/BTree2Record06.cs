﻿namespace PureHDF
{
    internal struct BTree2Record06 : IBTree2Record
    {
        #region Constructors

        public BTree2Record06(H5BaseReader reader)
        {
            CreationOrder = reader.ReadUInt64();
            HeapId = reader.ReadBytes(7);
        }

        #endregion

        #region Properties

        public ulong CreationOrder { get; set; }
        public byte[] HeapId { get; set; }

        #endregion
    }
}
