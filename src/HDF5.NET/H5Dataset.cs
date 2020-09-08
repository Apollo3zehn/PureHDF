using System.Diagnostics;

namespace HDF5.NET
{
    [DebuggerDisplay("{Name}: Class = '{DataType.Class}'")]
    public class H5Dataset : H5AttributableLink
    {
        #region Constructors

        internal H5Dataset(H5File file, string name, ObjectHeader objectHeader) 
            : base(file, name, objectHeader)
        {
            foreach (var message in this.ObjectHeader.HeaderMessages)
            {
                var type = message.GetType();

                if (type == typeof(DataLayoutMessage))
                    this.DataLayout = (DataLayoutMessage)message.Data;

                else if (type == typeof(DataspaceMessage))
                    this.Dataspace = (DataspaceMessage)message.Data;

                else if (type == typeof(DatatypeMessage))
                    this.DataType = (DatatypeMessage)message.Data;

                else if (type == typeof(FillValueMessage))
                    this.FillValue = (FillValueMessage)message.Data;

                else if (type == typeof(ObjectModificationMessage))
                    this.ObjectModification = (ObjectModificationMessage)message.Data;
            }
        }

        #endregion

        #region Properties

        public DataLayoutMessage DataLayout { get; }

        public DataspaceMessage Dataspace { get; }

        public DatatypeMessage DataType { get; }

        public FillValueMessage FillValue { get; }

        public ObjectModificationMessage ObjectModification { get; }

        #endregion
    }
}
