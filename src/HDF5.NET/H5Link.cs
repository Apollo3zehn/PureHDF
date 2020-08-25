using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public abstract class H5Link
    {
        #region Fields

        private List<H5Attribute> _attributes;

        #endregion

        #region Constructors

        internal H5Link(string name, ObjectHeader objectHeader, Superblock superblock)
        {
            this.Name = name;
            this.ObjectHeader = objectHeader;
            this.Superblock = superblock;
        }

        #endregion

        #region Properties

        public string Name { get; }

        public ObjectHeader ObjectHeader { get; }

        public Superblock Superblock { get; }

        public List<H5Attribute> Attributes
        {
            get
            {
                if (_attributes == null)
                    _attributes = this.GetAttributes();

                return _attributes;
            }
        }

        #endregion

        #region Methods

        private List<H5Attribute> GetAttributes()
        {
            return this.ObjectHeader
                .GetHeaderMessages<AttributeMessage>()
                .Select(message => new H5Attribute(message, this.Superblock))
                .ToList();
        }

        #endregion
    }
}
