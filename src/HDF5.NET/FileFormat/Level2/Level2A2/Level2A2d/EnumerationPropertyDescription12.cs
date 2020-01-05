using System.Collections.Generic;

namespace HDF5.NET
{
    public class EnumerationPropertyDescription12 : DatatypePropertyDescription
    {
        #region Constructors

        public EnumerationPropertyDescription12()
        {
            //
        }

        #endregion

        #region Properties

        public ulong BaseType { get; set; } // probably wrong
        public List<string> Names { get; set; }
        public List<byte[]> Values { get; set; }

        #endregion
    }
}