using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}")]
    public abstract class H5Link
    {
        #region Fields

        private List<H5Attribute> _attributes;

        #endregion

        #region Constructors

        internal H5Link(NamedObject namedObject, Superblock superblock)
        {
            this.Name = namedObject.Name;
            this.ObjectHeader = namedObject.Header;
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
            var attributeMessages = this.ObjectHeader.GetMessages<AttributeMessage>();
            var attributeInfoMessages = this.ObjectHeader.GetMessages<AttributeInfoMessage>();

            var attributes = this.ObjectHeader
                .GetMessages<AttributeMessage>()
                .Select(message => new H5Attribute(message, this.Superblock));

            var moreAttributes = attributeInfoMessages
                    .SelectMany(message => this.GetAttributesFromAttributeInfo(message));

            attributes = attributes.Concat(moreAttributes);
            return attributes.ToList();
        }

        private IEnumerable<H5Attribute> GetAttributesFromAttributeInfo(AttributeInfoMessage message)
        {
#error Implement this.
            throw new NotImplementedException();
        }

        #endregion
    }
}
