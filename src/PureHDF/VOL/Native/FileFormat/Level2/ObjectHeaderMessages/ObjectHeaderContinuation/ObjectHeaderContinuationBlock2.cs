using System.Text;

namespace PureHDF.VOL.Native;

internal record class ObjectHeaderContinuationBlock2(
    ulong Address
) : ObjectHeader(Address)
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OCHK");
    
    internal static ObjectHeaderContinuationBlock2 Decode(
        NativeContext context, 
        ulong objectHeaderSize,
        byte version, 
        bool withCreationOrder)
    {
        // address
        var address = (ulong)context.Driver.Position;

        // signature
        var signature = context.Driver.ReadBytes(4);
        Utils.ValidateSignature(signature, Signature);

        // TODO: H5OCache.c (L. 1595)  /* Gaps should only occur in chunks with no null messages */
        // TODO: read gap and checksum

        var objectHeader = new ObjectHeaderContinuationBlock2(
            address
        );

        // header messages
        objectHeader.InitializeHeaderMessages(
            context, 
            objectHeaderSize - 8,
            version, 
            withCreationOrder);

        return objectHeader;
    }
}