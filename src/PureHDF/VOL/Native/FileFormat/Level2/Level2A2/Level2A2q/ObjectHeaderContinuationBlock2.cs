﻿using System.Text;

namespace PureHDF.VOL.Native;

internal class ObjectHeaderContinuationBlock2 : ObjectHeader
{
    #region Constructors

    internal ObjectHeaderContinuationBlock2(H5Context context, ulong objectHeaderSize, byte version, bool withCreationOrder)
        : base(context)
    {
        // signature
        var signature = context.Driver.ReadBytes(4);
        Utils.ValidateSignature(signature, ObjectHeaderContinuationBlock2.Signature);

        // header messages
        var messages = ReadHeaderMessages(context, objectHeaderSize - 8, version, withCreationOrder);
        HeaderMessages.AddRange(messages);

        // TODO: H5OCache.c (L. 1595)  /* Gaps should only occur in chunks with no null messages */
        // TODO: read gap and checksum
    }

    #endregion

    #region Properties

    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OCHK");

    public uint Checksum { get; set; }

    #endregion
}