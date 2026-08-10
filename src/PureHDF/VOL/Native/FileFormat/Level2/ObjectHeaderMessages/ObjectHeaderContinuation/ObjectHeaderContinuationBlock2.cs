using System.Text;

namespace PureHDF.VOL.Native;

internal record class ObjectHeaderContinuationBlock2(
    ulong Address,
    List<HeaderMessage> HeaderMessages
) : ObjectHeader(Address, HeaderMessages)
{
    public static byte[] Signature { get; } = Encoding.ASCII.GetBytes("OCHK");

    internal static async ValueTask<ObjectHeaderContinuationBlock2> Decode(
        NativeReadContext context,
        ulong objectHeaderAddress,
        ulong objectHeaderSize,
        byte version,
        bool withCreationOrder)
    {
        // address
        var address = (ulong)context.Driver.Position;

        // signature
        var signature = await context.Driver.ReadBytes(4).ConfigureAwait(false);
        MathUtils.ValidateSignature(signature, Signature);

        // TODO: H5OCache.c (L. 1595)  /* Gaps should only occur in chunks with no null messages */
        // TODO: read gap and checksum

        // header messages
        var headerMessages = await ReadHeaderMessages(
            context,
            objectHeaderAddress,
            objectHeaderSize - 8,
            version,
            withCreationOrder).ConfigureAwait(false);

        var objectHeader = new ObjectHeaderContinuationBlock2(
            address,
            headerMessages
        );

        return objectHeader;
    }
}