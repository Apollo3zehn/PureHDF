using System.Collections;
using System.Reflection;
using System.Text;

namespace PureHDF.VOL.Native;

internal partial record class AttributeMessage
{
    private static readonly MethodInfo _methodInfoCreateAttributeMessage = typeof(AttributeMessage)
        .GetMethod(nameof(InternalCreate), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static AttributeMessage Create(
        NativeWriteContext context,
        string name,
        object attribute)
    {
        var data = attribute is H5Attribute h5Attribute
            ? h5Attribute.Data
            : attribute;

        var (elementType, isScalar) = WriteUtils.GetElementType(data.GetType());

        // TODO cache this
        var method = _methodInfoCreateAttributeMessage.MakeGenericMethod(data.GetType(), elementType);

        return (AttributeMessage)method.Invoke(default, new object?[] { context, name, attribute, data, isScalar })!;
    }

    private static AttributeMessage InternalCreate<T, TElement>(
        NativeWriteContext context,
        string name,
        object attribute,
        object data,
        bool isScalar)
    {
        var (memoryData, memoryDims)
            = WriteUtils.ToMemory<T, TElement>(data ?? throw new Exception("This should never happen."));

        var type = memoryData.GetType();

        /* datatype */
        var (datatype, encode) =
            DatatypeMessage.Create(context, memoryData, isScalar);

        /* dataspace */
        var dataspace = attribute is H5Attribute h5Attribute

            ? DataspaceMessage.Create(
                fileDims: h5Attribute.Dimensions ?? memoryDims
                    ?? throw new Exception("This should never happen."))

            : DataspaceMessage.Create(
                fileDims: memoryDims
                    ?? throw new Exception("This should never happen."));

        /* validation */
        var fileTotalSize = dataspace.Dimensions
            .Aggregate(1UL, (x, y) => x * y);

        var memoryTotalSize = (memoryDims ?? throw new Exception("This should never happen."))
            .Aggregate(1UL, (x, y) => x * y);

        if (memoryDims.Any() && fileTotalSize != memoryTotalSize)
            throw new Exception("The actual number of elements does not match the total number of elements given in the dimensions parameter.");

        // attribute
        // TODO avoid creation of system memory stream too often
        var dataEncodeSize = datatype.Size * dataspace.Dimensions
            .Aggregate(1UL, (product, dimension) => product * dimension); ;

        var buffer = new byte[dataEncodeSize];
        var localWriter = new SystemMemoryStream(buffer);

        var attributeMessage = new AttributeMessage(
            Flags: AttributeMessageFlags.None,
            Name: name,
            Datatype: datatype,
            Dataspace: dataspace,
            InputData: default,
            EncodeData: driver =>
            {
                encode(memoryData, localWriter);
                driver.Write(buffer);
            }
        )
        {
            Version = 3
        };

        return attributeMessage;
    }

    public override ushort GetEncodeSize()
    {
        if (Version != 3)
            throw new Exception("Only version 3 attribute messages are supported.");

        var nameEncodeSize = Encoding.UTF8.GetBytes(Name).Length + 1;
        var dataSize = Datatype.Size * Dataspace.Dimensions.Aggregate(1UL, (product, dimension) => product * dimension);

        // TODO: make this more exact?
        if (dataSize > 64 * 1024)
            throw new Exception("The maximum attribute size is 64KB.");

        var size =
            sizeof(byte) +
            sizeof(byte) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(ushort) +
            sizeof(byte) +
            nameEncodeSize +
            Datatype.GetEncodeSize() +
            Dataspace.GetEncodeSize() +
            (ushort)dataSize;

        return (ushort)size;
    }

    public override void Encode(H5DriverBase driver)
    {
        // version
        driver.Write(Version);

        // flags
        if (Version == 1)
            driver.Seek(1, SeekOrigin.Current);

        else
            driver.Write((byte)Flags);

        // name size
        var nameBytes = Encoding.UTF8.GetBytes(Name);
        driver.Write((ushort)(nameBytes.Length + 1));

        // datatype size
        var dataTypeEncodeSize = Datatype.GetEncodeSize();
        driver.Write(dataTypeEncodeSize);

        // dataspace size
        var dataSpaceEncodeSize = Dataspace.GetEncodeSize();
        driver.Write(dataSpaceEncodeSize);

        // name character set encoding
        if (Version == 3)
            driver.Write((byte)CharacterSetEncoding.UTF8);

        // name
        if (Version == 1)
        {
            throw new NotImplementedException() /* Version 1 requires padding */;
        }

        else
        {
            driver.Write(nameBytes);
            driver.Write((byte)0);
        }

        // datatype
        Datatype.Encode(driver);

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // dataspace
        Dataspace.Encode(driver);

        if (Version == 1)
            throw new NotImplementedException() /* Version 1 requires padding */;

        // data
        EncodeData.Invoke(driver);
    }
}