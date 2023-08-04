using System.Text;

namespace PureHDF.VOL.Native;

internal record class ObjectHeaderContinuationBlock2(
    ulong Address,
    List<HeaderMessage> HeaderMessages
) : ObjectHeader(Address, HeaderMessages)
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OCHK");
    
    internal static ObjectHeaderContinuationBlock2 Decode(
        NativeReadContext context, 
        ulong objectHeaderAddress,
        ulong objectHeaderSize,
        byte version, 
        bool withCreationOrder)
    {
        // address
        var address = (ulong)context.Driver.Position;

        // signature
        var signature = context.Driver.ReadBytes(4);
        MathUtils.ValidateSignature(signature, Signature);

        // TODO: H5OCache.c (L. 1595)  /* Gaps should only occur in chunks with no null messages */
        // TODO: read gap and checksum

        // header messages
        var headerMessages = ReadHeaderMessages(
            context,
            objectHeaderAddress,
            objectHeaderSize - 8,
            version, 
            withCreationOrder);

        var objectHeader = new ObjectHeaderContinuationBlock2(
            address,
            headerMessages
        );

        return objectHeader;
    }
}