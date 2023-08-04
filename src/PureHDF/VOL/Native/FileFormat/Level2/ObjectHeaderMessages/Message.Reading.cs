namespace PureHDF.VOL.Native;

internal abstract partial record class Message
{
    public static T Decode<T>(
        NativeReadContext context,
        ulong address,
        MessageFlags messageFlags, 
        Func<T> decode) where T : Message
    {
        // H5OShared.h (H5O_SHARED_DECODE)
        if (messageFlags.HasFlag(MessageFlags.Shared))
        {
            var sharedMessage = SharedMessage.Decode(context);
            return DecodeSharedMessage<T>(context, address, sharedMessage);
        }

        else
        {
            return decode();
        }
    }

    private static T DecodeSharedMessage<T>(NativeReadContext context, ulong address, SharedMessage message) where T : Message
    {
        // H5Oshared.c (H5O__shared_read)

        /* Check for implicit shared object header message*/
        if (message.Type == SharedMessageLocation.SharedObjectHeaderMessageHeap)
        {
            // TODO: Implement 
            throw new NotImplementedException("This code path is not yet implemented.");
        }
        else
        {
            if (message.Address == address)
            {
                /* The shared message is in the already opened object header.  This
                 * is possible, for example, if an attribute's datatype is shared in
                 * the same object header the attribute is in.  Read the message
                 * directly. */
                // TODO: Implement 
                throw new NotImplementedException("This code path is not yet implemented.");
            }
            else
            {
                /* The shared message is in another object header */

                // TODO: This would greatly benefit from a caching mechanism!
                var currentAddress = context.Driver.Position;
                context.Driver.Seek((long)message.Address, SeekOrigin.Begin);

                var header = ObjectHeader.Construct(context);
                var sharedMessage = header.GetMessage<T>();

                context.Driver.Seek(currentAddress, SeekOrigin.Begin);

                return sharedMessage;
            }
        }
    }
}