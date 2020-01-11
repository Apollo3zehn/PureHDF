using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public static ObjectHeader Construct(BinaryReader reader, Superblock superblock)
        {
            // get version
            var version = reader.ReadByte();

            // must be a version 2+ object header
            if (version != 1)
            {
                var signature = new byte[] { version }.Concat(reader.ReadBytes(3)).ToArray();
                H5Utils.ValidateSignature(signature, ObjectHeader2.Signature);
                version = reader.ReadByte();
            }

            return version switch
            {
                1 => new ObjectHeader1(version, reader, superblock),
                2 => new ObjectHeader2(version, reader),
                _ => throw new NotSupportedException($"The object header version '{version}' is not supported.")
            };
        }

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
