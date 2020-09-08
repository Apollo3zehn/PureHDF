using System.Collections.Generic;

namespace HDF5.NET
{
    public class EnumerationPropertyDescription3 : DatatypePropertyDescription
    {
        #region Constructors

        public EnumerationPropertyDescription3(H5BinaryReader reader, uint valueSize, ushort memberCount) : base(reader)
        {
            // base type
            this.BaseType = new DatatypeMessage(reader);

            // names
            this.Names = new List<string>((int)memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                this.Names.Add(H5Utils.ReadNullTerminatedString(reader, pad: false));
            }

            // values
            this.Values = new List<byte[]>(memberCount);

            for (int i = 0; i < memberCount; i++)
            {
                this.Values.Add(reader.ReadBytes((int)valueSize));
            }
        }

        #endregion

        #region Properties

        public DatatypeMessage BaseType { get; set; }
        public List<string> Names { get; set; }
        public List<byte[]> Values { get; set; }

        #endregion
    }
}