using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;

namespace HDF5.NET
{
    public abstract class ObjectHeader : FileBlock
    {
        #region Constructors

        public ObjectHeader(BinaryReader reader) : base(reader)
        {
            //
        }

        #endregion

        #region Properties

        public List<HeaderMessage> HeaderMessages { get; set; }

        #endregion

        #region Methods

        public override void Print(ILogger logger)
        {
            logger.LogInformation("ObjectHeader");

            for (int i = 0; i < this.HeaderMessages.Count; i++)
            {
                logger.LogInformation($"ObjectHeader HeaderMessage[{i}]");
                var message = this.HeaderMessages[i];
                message.Print(logger);

                if (message.Type == HeaderMessageType.SymbolTable)
                {
                    var symbolTableMessage = (SymbolTableMessage)message.Data;
                    symbolTableMessage.Print(logger);
                }
                else
                {
                    logger.LogInformation($"ObjectHeader HeaderMessage Type = {message.Type} is not supported yet. Stopping.");
                }
            }
        }

        #endregion
    }
}
